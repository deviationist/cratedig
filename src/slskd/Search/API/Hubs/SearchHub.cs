// <copyright file="SearchHub.cs" company="JP Dillingham">
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

namespace slskd.Search.API
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.SignalR;

    public static class SearchHubMethods
    {
        public static readonly string List = "LIST";
        public static readonly string Create = "CREATE";
        public static readonly string Responses = "RESPONSES";
        public static readonly string Update = "UPDATE";
        public static readonly string Delete = "DELETE";
    }

    /// <summary>
    ///     Extension methods for the search SignalR hub.
    /// </summary>
    public static class SearchHubExtensions
    {
        /// <summary>
        ///     Broadcast an update for a search.
        /// </summary>
        /// <remarks>
        ///     Responses are removed prior to sending.
        /// </remarks>
        /// <param name="hub">The hub.</param>
        /// <param name="search">The search to broadcast.</param>
        /// <returns>The operation context.</returns>
        public static Task BroadcastUpdateAsync(this IHubContext<SearchHub> hub, Search search)
        {
            return hub.Clients.All.SendAsync(SearchHubMethods.Update, search with { Responses = null });
        }

        /// <summary>
        ///     Gets the name of the group whose members receive the responses of the specified search as they arrive.
        /// </summary>
        /// <param name="searchId">The ID of the search.</param>
        /// <returns>The group name.</returns>
        public static string ResponseGroup(Guid searchId) => $"search-responses-{searchId}";

        /// <summary>
        ///     Send newly received responses to the clients subscribed to the search.
        /// </summary>
        /// <remarks>
        ///     Only subscribers receive them: a response carries a user's whole file list, which is too much to send to
        ///     every client for every search.
        /// </remarks>
        /// <param name="hub">The hub.</param>
        /// <param name="searchId">The ID of the search associated with the responses.</param>
        /// <param name="chunk">The responses, and the position of the first.</param>
        /// <returns>The operation context.</returns>
        public static Task SendResponsesAsync(this IHubContext<SearchHub> hub, Guid searchId, ResponseChunk chunk)
        {
            return hub.Clients.Group(ResponseGroup(searchId)).SendAsync(
                SearchHubMethods.Responses,
                new { searchId, from = chunk.From, responses = chunk.Responses });
        }

        /// <summary>
        ///     Tell the clients subscribed to the search that it has ended and its responses are saved.
        /// </summary>
        /// <remarks>
        ///     Sent on the same channel as the responses, and after the last of them, so a subscriber holding
        ///     <paramref name="total"/> responses knows it holds them all. It is also a second signal that the search
        ///     has ended, independent of the search's own update.
        /// </remarks>
        /// <param name="hub">The hub.</param>
        /// <param name="searchId">The ID of the search.</param>
        /// <param name="total">The number of responses the search received.</param>
        /// <returns>The operation context.</returns>
        public static Task SendResponsesCompleteAsync(this IHubContext<SearchHub> hub, Guid searchId, int total)
        {
            return hub.Clients.Group(ResponseGroup(searchId)).SendAsync(
                SearchHubMethods.Responses,
                new { searchId, from = total, responses = Array.Empty<Response>(), complete = true, total });
        }

        /// <summary>
        ///     Broadcast the creation of a new search.
        /// </summary>
        /// <remarks>
        ///     Responses are removed prior to sending.
        /// </remarks>
        /// <param name="hub">The hub.</param>
        /// <param name="search">The search to broadcast.</param>
        /// <returns>The operation context.</returns>
        public static Task BroadcastCreateAsync(this IHubContext<SearchHub> hub, Search search)
        {
            return hub.Clients.All.SendAsync(SearchHubMethods.Create, search with { Responses = null });
        }

        /// <summary>
        ///     Broadcast the deletion of a search.
        /// </summary>
        /// <remarks>
        ///     Responses are removed prior to sending.
        /// </remarks>
        /// <param name="hub">The hub.</param>
        /// <param name="search">The search to broadcast.</param>
        /// <returns>The operation context.</returns>
        public static Task BroadcastDeleteAsync(this IHubContext<SearchHub> hub, Search search)
        {
            return hub.Clients.All.SendAsync(SearchHubMethods.Delete, search with { Responses = null });
        }
    }

    /// <summary>
    ///     The search SignalR hub.
    /// </summary>
    [Authorize(Policy = AuthPolicy.Any)]
    public class SearchHub : Hub
    {
        public SearchHub(
            ISearchService searchService)
        {
            Searches = searchService;
        }

        private ISearchService Searches { get; }

        public override async Task OnConnectedAsync()
        {
            var searches = await Searches.ListAsync();
            await Clients.Caller.SendAsync(SearchHubMethods.List, searches);
        }

        /// <summary>
        ///     Subscribes the caller to the responses of a search in progress.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Returns the responses received so far, numbered from 0; the ones that follow arrive as
        ///         <see cref="SearchHubMethods.Responses"/> messages, each numbered from where it starts. The last
        ///         is marked complete and carries the total, once the responses are saved.
        ///     </para>
        ///     <para>
        ///         The caller joins the group before the snapshot is taken, so nothing falls between the two; a
        ///         message may instead overlap the snapshot, or arrive before it, and the numbering is what lets the
        ///         client discard the overlap.
        ///     </para>
        /// </remarks>
        /// <param name="searchId">The ID of the search.</param>
        /// <returns>
        ///     The responses so far, or null if the search is not in progress: it has ended and its responses are
        ///     saved, or it does not exist.
        /// </returns>
        public async Task<object> Subscribe(Guid searchId)
        {
            if (Program.IsRelayAgent)
            {
                throw new HubException("Searches are not available on a relay agent");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, SearchHubExtensions.ResponseGroup(searchId));

            var snapshot = Searches.GetLiveResponses(searchId);

            if (snapshot == null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, SearchHubExtensions.ResponseGroup(searchId));
                return null;
            }

            return new { searchId, from = snapshot.From, responses = snapshot.Responses };
        }

        /// <summary>
        ///     Unsubscribes the caller from the responses of a search.
        /// </summary>
        /// <param name="searchId">The ID of the search.</param>
        /// <returns>The operation context.</returns>
        public Task Unsubscribe(Guid searchId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, SearchHubExtensions.ResponseGroup(searchId));
        }
    }
}