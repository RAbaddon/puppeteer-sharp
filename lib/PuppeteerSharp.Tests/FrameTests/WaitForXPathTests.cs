﻿using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace PuppeteerSharp.Tests.FrameTests
{
    [Collection("PuppeteerLoaderFixture collection")]
    public class WaitForXPathTests : PuppeteerPageBaseTest
    {
        const string addElement = "tag => document.body.appendChild(document.createElement(tag))";

        public WaitForXPathTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ShouldSupportSomeFancyXpath()
        {
            await Page.SetContentAsync("<p>red herring</p><p>hello  world  </p>");
            var waitForXPath = Page.WaitForXPathAsync("//p[normalize-space(.)=\"hello world\"]");
            Assert.Equal("hello  world  ", await Page.EvaluateFunctionAsync("x => x.textContent", await waitForXPath));
        }

        [Fact]
        public async Task ShouldRunInSpecifiedFrame()
        {
            await FrameUtils.AttachFrameAsync(Page, "frame1", TestConstants.EmptyPage);
            await FrameUtils.AttachFrameAsync(Page, "frame2", TestConstants.EmptyPage);
            var frame1 = Page.Frames[1];
            var frame2 = Page.Frames[2];
            var added = false;
            var waitForXPathPromise = frame2.WaitForXPathAsync("//div").ContinueWith(_ => added = true);
            Assert.False(added);
            await frame1.EvaluateFunctionAsync(addElement, "div");
            Assert.False(added);
            await frame2.EvaluateFunctionAsync(addElement, "div");
            await waitForXPathPromise;
        }

        [Fact]
        public async Task ShouldThrowIfEvaluationFailed()
        {
            await Page.EvaluateOnNewDocumentAsync(@"function() {
                document.evaluate = null;
            }");
            await Page.GoToAsync(TestConstants.EmptyPage);
            var exception = await Assert.ThrowsAsync<EvaluationFailedException>(()
                => Page.WaitForXPathAsync("*"));
            Assert.Contains("document.evaluate is not a function", exception.Message);
        }

        [Fact]
        public async Task ShouldThrowWhenFrameIsDetached()
        {
            await FrameUtils.AttachFrameAsync(Page, "frame1", TestConstants.EmptyPage);
            var frame = Page.Frames[1];
            var waitPromise = frame.WaitForXPathAsync("//*[@class=\"box\"]");
            await FrameUtils.DetachFrameAsync(Page, "frame1");
            var exception = await Assert.ThrowsAnyAsync<Exception>(() => waitPromise);
            Assert.Contains("waitForFunction failed: frame got detached.", exception.Message);
        }

        [Fact]
        public async Task HiddenShouldWaitForDisplayNone()
        {
            var divHidden = false;
            await Page.SetContentAsync("<div style='display: block;'></div>");
            var waitForXPath = Page.WaitForXPathAsync("//div", new WaitForSelectorOptions { Hidden = true })
                .ContinueWith(_ => divHidden = true);
            await Page.WaitForXPathAsync("//div"); // do a round trip
            Assert.False(divHidden);
            await Page.EvaluateExpressionAsync("document.querySelector('div').style.setProperty('display', 'none')");
            Assert.True(await waitForXPath);
            Assert.True(divHidden);
        }

        [Fact]
        public async Task ShouldReturnTheElementHandle()
        {
            var waitForXPath = Page.WaitForXPathAsync("//*[@class=\"zombo\"]");
            await Page.SetContentAsync("<div class='zombo'>anything</div>");
            Assert.Equal("anything", await Page.EvaluateFunctionAsync<string>("x => x.textContent", await waitForXPath));
        }

        [Fact]
        public async Task ShouldAllowYouToSelectATextNode()
        {
            await Page.SetContentAsync("<div>some text</div>");
            var text = await Page.WaitForXPathAsync("//div/text()");
            Assert.Equal(3 /* Node.TEXT_NODE */, await (await text.GetPropertyAsync("nodeType")).JsonValueAsync<int>());
        }

        [Fact]
        public async Task ShouldAllowYouToSelectAnElementWithSingleSlash()
        {
            await Page.SetContentAsync("<div>some text</div>");
            var waitForXPath = Page.WaitForXPathAsync("/html/body/div");
            Assert.Equal("some text", await Page.EvaluateFunctionAsync<string>("x => x.textContent", await waitForXPath));
        }
    }
}
