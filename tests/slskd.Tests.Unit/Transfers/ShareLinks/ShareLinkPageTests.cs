// <copyright file="ShareLinkPageTests.cs" company="Robert S.">
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

namespace slskd.Tests.Unit.Transfers.ShareLinks;

using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using slskd.Transfers.ShareLinks;
using slskd.Transfers.ShareLinks.API;
using Xunit;

public class ShareLinkPageTests
{
    private const string Page = "<html><head><title>t</title></head><body></body></html>";

    [Fact]
    public void Adds_The_Markup_At_The_End_Of_The_Head()
    {
        var html = ShareLinkPage.WithHead(Page, "  <script src=\"https://s.example/a.js\"></script>\n");

        Assert.StartsWith("<html><head><title>t</title><script src=\"https://s.example/a.js\"></script>", html);
        Assert.EndsWith("</head><body></body></html>", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Leaves_The_Page_Alone_Without_Markup(string head)
    {
        Assert.Equal(Page, ShareLinkPage.WithHead(Page, head));
    }

    [Fact]
    public void Leaves_A_Page_Without_A_Head_Alone()
    {
        Assert.Equal("<p>x</p>", ShareLinkPage.WithHead("<p>x</p>", "<script>a()</script>"));
    }

    [Fact]
    public void An_Added_Inline_Script_Is_Allowed_By_The_Policy()
    {
        var html = ShareLinkPage.WithHead(Page, "<script>window.track()</script>");

        Assert.Contains("'sha256-", ShareLinkPagePolicy.For(html));
    }

    [Fact]
    public void Nothing_Is_Injected_Into_The_Page_After_Its_Policy_Is_Made()
    {
        // the urlBase script would arrive after the policy was worked out from the page, and be refused by the browser
        Assert.Contains(ShareLinkPage.PathPrefix, Program.HTMLInjectionExcludedRoutes);

        // and the prefix is the page's real route, so renaming one cannot quietly undo the other
        var route = typeof(ShareLinksController).GetMethod(nameof(ShareLinksController.Page))
            .GetCustomAttributes<HttpGetAttribute>().Single().Template;

        Assert.StartsWith(ShareLinkPage.PathPrefix.TrimStart('/'), route);
    }
}
