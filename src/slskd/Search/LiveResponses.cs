// <copyright file="LiveResponses.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Search
{
    using System.Collections.Generic;

    /// <summary>
    ///     A run of a search's responses, starting at position <see cref="From"/>.
    /// </summary>
    /// <param name="From">The position of the first response in the run.</param>
    /// <param name="Responses">The responses.</param>
    public record ResponseChunk(int From, IReadOnlyList<Response> Responses);

    /// <summary>
    ///     The responses of a search in progress, in the order they arrived.
    /// </summary>
    /// <remarks>
    ///     Responses are only ever appended, so a response's position never changes: a client holding the first
    ///     n responses holds exactly those, and needs only what comes from position n on. That is what lets a
    ///     client combine a snapshot with the chunks streamed around it without gaps or duplicates.
    /// </remarks>
    public sealed class LiveResponses
    {
        private readonly object syncRoot = new();
        private readonly List<Response> responses = new();
        private int taken;

        /// <summary>
        ///     Gets the number of responses received so far.
        /// </summary>
        public int Count
        {
            get
            {
                lock (syncRoot)
                {
                    return responses.Count;
                }
            }
        }

        /// <summary>
        ///     Appends a response. Safe to call from many threads at once.
        /// </summary>
        /// <param name="response">The response.</param>
        public void Add(Response response)
        {
            lock (syncRoot)
            {
                responses.Add(response);
            }
        }

        /// <summary>
        ///     Returns every response received so far, from position 0.
        /// </summary>
        /// <returns>The responses.</returns>
        public ResponseChunk Snapshot()
        {
            lock (syncRoot)
            {
                return new ResponseChunk(0, responses.ToArray());
            }
        }

        /// <summary>
        ///     Returns the responses received since the last call, or null if there are none.
        /// </summary>
        /// <returns>The responses not yet taken, or null.</returns>
        public ResponseChunk TakeNew()
        {
            lock (syncRoot)
            {
                if (taken == responses.Count)
                {
                    return null;
                }

                var chunk = new ResponseChunk(taken, responses.GetRange(taken, responses.Count - taken).ToArray());
                taken = responses.Count;
                return chunk;
            }
        }
    }
}
