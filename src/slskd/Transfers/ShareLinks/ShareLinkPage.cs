// <copyright file="ShareLinkPage.cs" company="Robert S.">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) 2026 Robert S.
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
//   │ SPDX-FileCopyrightText: 2026 Robert S.
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Transfers.ShareLinks;

using System;

/// <summary>
///     The page a share link opens, as it is served.
/// </summary>
public static class ShareLinkPage
{
    /// <summary>
    ///     The path every link page is served under, ahead of its token.
    /// </summary>
    public const string PathPrefix = "/l/";

    /// <summary>
    ///     Returns the page with the specified markup added at the end of its head.
    /// </summary>
    /// <remarks>
    ///     At the end, so that it comes after the page's own stylesheet and can override it, and before the body, so a
    ///     script in it runs before the page's own. A page without a head gets nothing added rather than markup in a
    ///     place it was not written for.
    /// </remarks>
    /// <param name="html">The page.</param>
    /// <param name="head">The markup to add; nothing if null or blank.</param>
    /// <returns>The page with the markup added.</returns>
    public static string WithHead(string html, string head)
    {
        ArgumentNullException.ThrowIfNull(html);

        if (string.IsNullOrWhiteSpace(head))
        {
            return html;
        }

        var end = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);

        return end < 0 ? html : string.Concat(html.AsSpan(0, end), head.Trim(), "\n    ", html.AsSpan(end));
    }
}
