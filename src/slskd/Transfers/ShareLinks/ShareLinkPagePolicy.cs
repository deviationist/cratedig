// <copyright file="ShareLinkPagePolicy.cs" company="Robert S.">
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
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
///     The Content-Security-Policy the share link page is served with.
/// </summary>
/// <remarks>
///     <para>
///         The page is the one thing this instance shows to people who are not its operator, at whatever public name
///         it is given -- and that name is likely a sibling of the ones the operator signs in at, so a script run on it
///         would be same-site with them. It needs nothing but its own inline script and stylesheet and calls to its
///         own API, so the policy allows exactly those and nothing else: no other script, no plugin, no frame, no form.
///     </para>
///     <para>
///         The inline blocks are allowed by hash, computed from the page as it is served, so the policy cannot fall out
///         of step with the page: a hash written down anywhere else would refuse the page's own script the first time
///         the page changed, and the page would show nothing at all.
///     </para>
/// </remarks>
public static partial class ShareLinkPagePolicy
{
    /// <summary>
    ///     Returns the policy for the specified page.
    /// </summary>
    /// <param name="html">The page, exactly as it is served.</param>
    /// <param name="allow">Outside sources the operator has allowed, by kind; none if null.</param>
    /// <returns>The policy.</returns>
    public static string For(string html, Options.TransfersOptions.ShareLinksOptions.ShareLinkPageSourcesOptions allow = null)
    {
        ArgumentNullException.ThrowIfNull(html);

        return string.Join("; ", [
            "default-src 'none'",
            $"script-src {Sources(HashesOf(ScriptBlock(), html), allow?.Script)}",
            $"style-src {Sources(HashesOf(StyleBlock(), html), allow?.Style)}",
            $"img-src {Sources(["data:"], allow?.Img)}",
            $"font-src {Sources([], allow?.Font)}",
            $"connect-src {Sources(["'self'"], allow?.Connect)}",
            "base-uri 'none'",
            "form-action 'none'",
            "frame-ancestors 'none'",
        ]);
    }

    /// <summary>
    ///     Returns a value indicating whether the specified text is a source an operator may add to the policy: one
    ///     named host, over https or wss.
    /// </summary>
    /// <remarks>
    ///     Deliberately narrower than what CSP accepts. A source added here lets whatever it serves run on, or read
    ///     from, the one page of this instance that strangers open, with the link's token in its address. So it has
    ///     to name a host: no bare <c>*</c>, no scheme alone (<c>https:</c> is every site there is), no quoted keyword
    ///     (inline blocks are allowed by hash already, and <c>'unsafe-inline'</c> would undo that), and nothing over
    ///     plain http, which anyone on the path could replace. A subdomain wildcard (<c>https://*.example.com</c>) is
    ///     still one operator's domain. Nothing that could end a source list or a directive gets through, so an entry
    ///     can only ever add itself.
    /// </remarks>
    /// <param name="source">The text.</param>
    /// <returns>A value indicating whether it is such a source.</returns>
    public static bool IsSource(string source)
        => !string.IsNullOrEmpty(source) && HostSource().IsMatch(source);

    private static string Sources(IEnumerable<string> own, IEnumerable<string> allowed)
    {
        var sources = own.Concat((allowed ?? []).Where(IsSource)).Distinct().ToList();

        // a directive with nothing in it allows nothing
        return sources.Count > 0 ? string.Join(' ', sources) : "'none'";
    }

    private static List<string> HashesOf(Regex block, string html)
        => block.Matches(html)
            .Select(match => $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(match.Groups[1].Value)))}'")
            .Distinct()
            .ToList();

    // inline blocks, attributes or not -- markup added to the page may write <script type="module"> -- but not one with
    // a src: its content is never run, and what it loads is allowed by source, not by hash
    [GeneratedRegex("<script(?![^>]*\\bsrc\\s*=)(?:\\s[^>]*)?>(.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptBlock();

    [GeneratedRegex("<style(?:\\s[^>]*)?>(.*?)</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StyleBlock();

    // https://(*.)host.name[:port][/path], or wss://; the host has at least two labels, so * and a bare word are out
    [GeneratedRegex("^(?:https|wss)://(?:\\*\\.)?[a-z0-9](?:[a-z0-9-]*[a-z0-9])?(?:\\.[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)+(?::[0-9]{1,5})?(?:/[a-z0-9._~!$&()*+=:@%/-]*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex HostSource();
}
