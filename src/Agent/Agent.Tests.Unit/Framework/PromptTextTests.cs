// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;

namespace Agent.Tests.Unit.Framework
{
    public class PromptTextTests
    {
        [Fact]
        public void Constructor_NullValue_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new PromptText(null!));
        }

        [Fact]
        public void ToString_ReturnsValue_WhenNoOptionsSet()
        {
            var text = "Hello, world!";
            var prompt = new PromptText(text);

            var result = prompt.ToString();

            Assert.Contains(text, result);
        }

        [Fact]
        public void ToString_IncludesHandoffInstructions_WhenSet()
        {
            var prompt = new PromptText("Test").WithHandoffInstructions();

            var result = prompt.ToString();

            Assert.Contains("System context", result);
            Assert.Contains("Test", result);
        }

        [Fact]
        public void ImplicitOperator_String_ReturnsPromptTextString()
        {
            PromptText prompt = new PromptText("abc");
            string result = prompt;

            Assert.Contains("abc", result);
        }

        [Fact]
        public void ImplicitOperator_String_NullPromptText_ReturnsEmptyString()
        {
            PromptText prompt = null;
            string result = prompt;

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ImplicitOperator_PromptText_ReturnsPromptTextInstance()
        {
            string value = "test";
            PromptText prompt = value;

            Assert.NotNull(prompt);
            Assert.Equal(value, prompt.ToString().Trim());
        }

        [Fact]
        public void ImplicitOperator_PromptText_NullString_ReturnsNull()
        {
            string? value = null;
            PromptText? prompt = value;

            Assert.Null(prompt);
        }

        [Fact]
        public void WithHandoffInstructions_SetsProperty()
        {
            var prompt = new PromptText("abc");
            prompt.WithHandoffInstructions();

            Assert.True(prompt.HasHandoffInstructions);
        }

        [Fact]
        public void AddCommonPrompt_AddsPrompt()
        {
            var prompt = new PromptText("abc");
            prompt.AddCommonPrompt("def");

            Assert.Contains("def", prompt.ToString());
        }
    }
}
