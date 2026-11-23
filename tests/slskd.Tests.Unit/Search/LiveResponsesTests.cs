namespace slskd.Tests.Unit.Search
{
    using System.Linq;
    using System.Threading.Tasks;
    using slskd.Search;
    using Xunit;

    public class LiveResponsesTests
    {
        private static Response R(string username) => new() { Username = username };

        [Fact]
        public void TakeNew_Returns_Null_When_Nothing_Arrived()
        {
            var live = new LiveResponses();

            Assert.Null(live.TakeNew());
        }

        [Fact]
        public void TakeNew_Returns_Each_Response_Once_Numbered_From_Where_It_Starts()
        {
            var live = new LiveResponses();
            live.Add(R("a"));
            live.Add(R("b"));

            var first = live.TakeNew();

            live.Add(R("c"));

            var second = live.TakeNew();

            Assert.Equal(0, first.From);
            Assert.Equal(new[] { "a", "b" }, first.Responses.Select(r => r.Username));
            Assert.Equal(2, second.From);
            Assert.Equal(new[] { "c" }, second.Responses.Select(r => r.Username));
            Assert.Null(live.TakeNew());
        }

        [Fact]
        public void Snapshot_Returns_Everything_From_Zero_And_Does_Not_Affect_TakeNew()
        {
            var live = new LiveResponses();
            live.Add(R("a"));
            live.TakeNew();
            live.Add(R("b"));

            var snapshot = live.Snapshot();
            var next = live.TakeNew();

            Assert.Equal(0, snapshot.From);
            Assert.Equal(new[] { "a", "b" }, snapshot.Responses.Select(r => r.Username));
            Assert.Equal(1, next.From);
            Assert.Equal(new[] { "b" }, next.Responses.Select(r => r.Username));
        }

        [Fact]
        public void Snapshot_Is_Not_Changed_By_Later_Responses()
        {
            var live = new LiveResponses();
            live.Add(R("a"));

            var snapshot = live.Snapshot();
            live.Add(R("b"));

            Assert.Single(snapshot.Responses);
        }

        [Fact]
        public async Task Concurrent_Adds_Are_All_Taken_Exactly_Once_In_Order()
        {
            var live = new LiveResponses();

            var adding = Task.WhenAll(Enumerable.Range(0, 8).Select(t => Task.Run(() =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    live.Add(R($"{t}-{i}"));
                }
            })));

            var taken = new System.Collections.Generic.List<Response>();

            while (!adding.IsCompleted)
            {
                var chunk = live.TakeNew();

                if (chunk != null)
                {
                    Assert.Equal(taken.Count, chunk.From);
                    taken.AddRange(chunk.Responses);
                }
            }

            await adding;

            var last = live.TakeNew();

            if (last != null)
            {
                Assert.Equal(taken.Count, last.From);
                taken.AddRange(last.Responses);
            }

            Assert.Equal(8000, taken.Count);
            Assert.Equal(8000, taken.Select(r => r.Username).Distinct().Count());
            Assert.Equal(live.Snapshot().Responses.Select(r => r.Username), taken.Select(r => r.Username));
        }
    }
}
