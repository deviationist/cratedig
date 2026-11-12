// <copyright file="ShareLinkPagePolicyTests.cs" company="Robert S.">
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

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using slskd.Transfers.ShareLinks;
using Xunit;

public class ShareLinkPagePolicyTests
{
    [Fact]
    public void Allows_Exactly_The_Inline_Blocks_Of_The_Page()
    {
        var policy = ShareLinkPagePolicy.For("<style>a{}</style><script>go()</script>");

        Assert.Contains($"script-src {Hash("go()")};", policy);
        Assert.Contains($"style-src {Hash("a{}")};", policy);
        Assert.StartsWith("default-src 'none';", policy);
        Assert.Contains("connect-src 'self'", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
    }

    [Fact]
    public void A_Page_Without_A_Script_Allows_None()
    {
        Assert.Contains("script-src 'none';", ShareLinkPagePolicy.For("<p>nothing</p>"));
    }

    [Fact]
    public void Hashes_Inline_Blocks_With_Attributes_But_Not_Ones_With_A_Src()
    {
        var policy = ShareLinkPagePolicy.For("<script type=\"module\">a()</script><script defer src=\"https://x.example/s.js\">b()</script><style media=\"print\">p{}</style>");

        Assert.Contains($"script-src {Hash("a()")};", policy);
        Assert.DoesNotContain(Hash("b()"), policy);
        Assert.Contains($"style-src {Hash("p{}")};", policy);
    }

    [Fact]
    public void Adds_The_Allowed_Sources_To_Their_Directives()
    {
        var allow = new Options.TransfersOptions.ShareLinksOptions.ShareLinkPageSourcesOptions
        {
            Script = ["https://stats.example.com"],
            Connect = ["https://stats.example.com"],
            Img = ["https://pixel.example.com"],
            Font = ["https://fonts.example.com"],
        };

        var policy = ShareLinkPagePolicy.For("<script>go()</script>", allow);

        Assert.Contains($"script-src {Hash("go()")} https://stats.example.com;", policy);
        Assert.Contains("connect-src 'self' https://stats.example.com;", policy);
        Assert.Contains("img-src data: https://pixel.example.com;", policy);
        Assert.Contains("font-src https://fonts.example.com;", policy);
        Assert.Contains("style-src 'none';", policy);
    }

    [Fact]
    public void Without_Additions_The_Policy_Allows_Nothing_Outside()
    {
        var policy = ShareLinkPagePolicy.For("<p>nothing</p>");

        Assert.Contains("font-src 'none';", policy);
        Assert.Contains("connect-src 'self';", policy);
        Assert.Contains("img-src data:;", policy);
    }

    [Theory]
    [InlineData("https://stats.example.com")]
    [InlineData("https://*.example.com")]
    [InlineData("https://stats.example.com:8443/js/")]
    [InlineData("wss://live.example.com")]
    public void Takes_Named_Hosts_Over_Https(string source)
    {
        Assert.True(ShareLinkPagePolicy.IsSource(source));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*")]
    [InlineData("https:")]
    [InlineData("data:")]
    [InlineData("http://stats.example.com")]
    [InlineData("stats.example.com")]
    [InlineData("https://localhost")]
    [InlineData("javascript:alert(1)")]
    [InlineData("'unsafe-inline'")]
    [InlineData("'self'")]
    [InlineData("'sha256-abc='")]
    [InlineData("https://a.example; script-src *")]
    [InlineData("https://a.example https://b.example")]
    [InlineData("https://a.example,https://b.example")]
    [InlineData("https://a.example\nX")]
    [InlineData("")]
    [InlineData(null)]
    public void Refuses_Anything_But_One_Named_Host_Over_Https(string source)
    {
        Assert.False(ShareLinkPagePolicy.IsSource(source));
    }

    [Fact]
    public void Leaves_Out_A_Refused_Source_Even_If_Validation_Was_Skipped()
    {
        var allow = new Options.TransfersOptions.ShareLinksOptions.ShareLinkPageSourcesOptions { Script = ["'unsafe-inline'", "https://ok.example"] };

        var policy = ShareLinkPagePolicy.For("<p></p>", allow);

        Assert.Contains("script-src https://ok.example;", policy);
        Assert.DoesNotContain("unsafe-inline", policy);
    }

    [Fact]
    public void Validation_Names_A_Refused_Source()
    {
        var allow = new Options.TransfersOptions.ShareLinksOptions.ShareLinkPageSourcesOptions { Connect = ["'unsafe-eval'"] };

        var results = allow.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(allow)).ToList();

        Assert.Single(results);
        Assert.Contains("Connect", results[0].ErrorMessage);
    }

    [Fact]
    public void The_Shipped_Page_Is_Covered_By_Its_Policy()
    {
        var html = File.ReadAllText(PagePath());

        // every script and stylesheet the page has must be one the policy can allow: an inline block with no
        // attributes. anything else would be refused by the browser, and the page would quietly show nothing
        Assert.Single(Regex.Matches(html, "<script\\b"));
        Assert.Single(Regex.Matches(html, "<style\\b"));
        Assert.DoesNotMatch("<link\\b(?![^>]*rel=\"icon\"[^>]*href=\"data:)", html);
        Assert.DoesNotMatch("\\son[a-z]+=", html);
        Assert.DoesNotMatch("\\sstyle=", html);

        var script = Regex.Match(html, "<script>(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;

        Assert.Contains(Hash(script), ShareLinkPagePolicy.For(html));
    }

    private static string Hash(string block)
        => $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(block)))}'";

    private static string PagePath()
    {
        // walk up to the repository root. the page is in the web project's public folder in a checkout, but the image
        // build runs these tests with only the web project's build output, copied into wwwroot -- the same file
        string[][] candidates = [["src", "web", "public"], ["src", "slskd", "wwwroot"]];

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            foreach (var candidate in candidates)
            {
                var path = Path.Combine([directory.FullName, .. candidate, "share-link.html"]);

                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        throw new FileNotFoundException("share-link.html is in neither the web project nor wwwroot");
    }
}
