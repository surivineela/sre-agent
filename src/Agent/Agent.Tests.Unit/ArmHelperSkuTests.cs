// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Helpers;
using Xunit;

namespace Agent.Tests.Unit
{
    /// <summary>
    /// Unit tests for ArmHelper SKU handling methods
    /// </summary>
    public class ArmHelperSkuTests
    {
        [Theory]
        [InlineData("F1", "F", "Free")]
        [InlineData("P1v2", "Pv2", "PremiumV2")]
        [InlineData("B2", "B", "Basic")]
        [InlineData("S1", "S", "Standard")]
        [InlineData("I1v2", "Iv2", "IsolatedV2")]
        [InlineData("EP1", "EP", "ElasticPremium")]  // Unknown SKU
        [InlineData("Y1", "Y", "Dynamic")]           // Unknown SKU
        [InlineData("FC1", "FC", "Free")] // Unknown SKU - F prefix matches "Free" tier first
        [InlineData("XYZ123", "XYZ", "Custom")]      // Completely unknown SKU
        [InlineData("", "Unknown", "Unknown")]       // Empty SKU
        public void TestSkuFamilyAndTierExtraction(string sku, string expectedFamily, string expectedTier)
        {
            // Use reflection to call the private static methods
            var armHelperType = typeof(ArmHelper);
            
            var getFamilyMethod = armHelperType.GetMethod("GetFamilyFromSku", BindingFlags.NonPublic | BindingFlags.Static);
            var getTierMethod = armHelperType.GetMethod("GetTierFromSku", BindingFlags.NonPublic | BindingFlags.Static);
            
            Assert.NotNull(getFamilyMethod);
            Assert.NotNull(getTierMethod);
            
            // Test family extraction
            var actualFamily = getFamilyMethod.Invoke(null, new object[] { sku });
            Assert.Equal(expectedFamily, actualFamily);
            
            // Test tier extraction
            var actualTier = getTierMethod.Invoke(null, new object[] { sku });
            Assert.Equal(expectedTier, actualTier);
        }

        [Fact]
        public void TestSkuHandling_NullInput()
        {
            // Test null input separately
            var armHelperType = typeof(ArmHelper);
            
            var getFamilyMethod = armHelperType.GetMethod("GetFamilyFromSku", BindingFlags.NonPublic | BindingFlags.Static);
            var getTierMethod = armHelperType.GetMethod("GetTierFromSku", BindingFlags.NonPublic | BindingFlags.Static);
            
            Assert.NotNull(getFamilyMethod);
            Assert.NotNull(getTierMethod);
            
            // Test family extraction with null (should handle gracefully)
            var actualFamily = getFamilyMethod.Invoke(null, new object?[] { null });
            Assert.Equal("Unknown", actualFamily);
            
            // Test tier extraction with null (should handle gracefully)
            var actualTier = getTierMethod.Invoke(null, new object?[] { null });
            Assert.Equal("Unknown", actualTier);
        }

        [Fact]
        public void TestExtractFamilyFromUnknownSku_ValidPatterns()
        {
            var armHelperType = typeof(ArmHelper);
            var extractFamilyMethod = armHelperType.GetMethod("ExtractFamilyFromUnknownSku", BindingFlags.NonPublic | BindingFlags.Static);
            
            Assert.NotNull(extractFamilyMethod);
            
            // Test valid patterns
            Assert.Equal("EP", extractFamilyMethod.Invoke(null, new object[] { "EP1" }));
            Assert.Equal("FC", extractFamilyMethod.Invoke(null, new object[] { "FC1" }));
            Assert.Equal("Y", extractFamilyMethod.Invoke(null, new object[] { "Y1" }));
            Assert.Equal("ABC", extractFamilyMethod.Invoke(null, new object[] { "ABC123" }));
            
            // Test edge cases
            Assert.Equal("Unknown", extractFamilyMethod.Invoke(null, new object[] { "" }));
            Assert.Equal("NoNumberHe", extractFamilyMethod.Invoke(null, new object[] { "NoNumberHere" }));
            
            // Test truncation for very long SKUs
            var longSku = "VeryLongSkuNameThatExceedsTenCharacters";
            var result = extractFamilyMethod.Invoke(null, new object[] { longSku });
            Assert.Equal("VeryLongSk", result);
        }

        [Fact]
        public void TestExtractFamilyFromUnknownSku_NullInput()
        {
            var armHelperType = typeof(ArmHelper);
            var extractFamilyMethod = armHelperType.GetMethod("ExtractFamilyFromUnknownSku", BindingFlags.NonPublic | BindingFlags.Static);
            
            Assert.NotNull(extractFamilyMethod);
            
            // Test null input separately
            Assert.Equal("Unknown", extractFamilyMethod.Invoke(null, new object?[] { null }));
        }

        [Fact]
        public void TestDeriveeTierFromUnknownSku_ValidPatterns()
        {
            var armHelperType = typeof(ArmHelper);
            var deriveTierMethod = armHelperType.GetMethod("DeriveeTierFromUnknownSku", BindingFlags.NonPublic | BindingFlags.Static);
            
            Assert.NotNull(deriveTierMethod);
            
            // Test tier derivation patterns
            Assert.Equal("ElasticPremium", deriveTierMethod.Invoke(null, new object[] { "EP1" }));
            Assert.Equal("Dynamic", deriveTierMethod.Invoke(null, new object[] { "Y1" }));
            Assert.Equal("Free", deriveTierMethod.Invoke(null, new object[] { "FC1" })); // FC1 returns "Free" because F prefix is checked first
            Assert.Equal("Free", deriveTierMethod.Invoke(null, new object[] { "F999" }));
            Assert.Equal("Basic", deriveTierMethod.Invoke(null, new object[] { "B999" }));
            Assert.Equal("Standard", deriveTierMethod.Invoke(null, new object[] { "S999" }));
            Assert.Equal("PremiumV3", deriveTierMethod.Invoke(null, new object[] { "P1v3New" }));
            Assert.Equal("PremiumV2", deriveTierMethod.Invoke(null, new object[] { "P1v2New" }));
            Assert.Equal("Premium", deriveTierMethod.Invoke(null, new object[] { "P999" }));
            Assert.Equal("Isolated", deriveTierMethod.Invoke(null, new object[] { "I999" }));
            
            // Test edge cases
            Assert.Equal("Unknown", deriveTierMethod.Invoke(null, new object[] { "" }));
            Assert.Equal("Custom", deriveTierMethod.Invoke(null, new object[] { "UnknownPattern123" }));
        }

        [Fact]
        public void TestDeriveeTierFromUnknownSku_NullInput()
        {
            var armHelperType = typeof(ArmHelper);
            var deriveTierMethod = armHelperType.GetMethod("DeriveeTierFromUnknownSku", BindingFlags.NonPublic | BindingFlags.Static);
            
            Assert.NotNull(deriveTierMethod);
            
            // Test null input separately
            Assert.Equal("Unknown", deriveTierMethod.Invoke(null, new object?[] { null }));
        }
    }
}