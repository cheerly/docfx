﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Build.OverwriteDocuments.Tests
{
    using System.Collections.Generic;
    using System.Linq;

    using Markdig;
    using Markdig.Syntax;

    using Microsoft.DocAsCode.Build.OverwriteDocuments;
    using Microsoft.DocAsCode.Common;
    using Microsoft.DocAsCode.Tests.Common;

    using Xunit;

    [Trait("Owner", "jipe")]
    [Trait("EntityType", "OverwriteDocumentModelCreater")]
    public class OverwriteDocumentModelCreaterTest
    {
        private TestLoggerListener _listener = TestLoggerListener.CreateLoggerListenerWithPhaseEqualFilter("overwrite_document_model_creater");

        [Fact]
        public void YamlCodeBlockTest()
        {
            var yamlCodeBlockString = "a: b\nc: d\ne: f";
            var testYamlCodeBlock = Markdown.Parse(@"```
a: b
c: d
e: f
```")[0];
            var actual = OverwriteDocumentModelCreater.ConvertYamlCodeBlock(yamlCodeBlockString, testYamlCodeBlock);
            var expected = new Dictionary<string, object>()
            {
                {"a", "b"},
                {"c", "d"},
                {"e", "f"}
            };
            Assert.Equal(actual.ToString(), expected.ToString());
        }

        [Fact]
        public void ContentConvertTest()
        {
            var testBlockList = Markdown.Parse("Test").ToList();

            string[] testOPaths =
            {
                "summary",
                "return/description",
                "return/type",
                "function/parameters[id=\"para1\"]/description",
                "function/parameters[id=\"para1\"]/type",
                "function/parameters[id=\"para2\"]/description",
            };
            var contents = new List<MarkdownPropertyModel>();
            foreach (var item in testOPaths)
            {
                contents.Add(new MarkdownPropertyModel
                {
                    PropertyName = item,
                    PropertyNameSource = Markdown.Parse($"## `{item}`")[0],
                    PropertyValue = testBlockList
                });
            }

            var contentsMetadata = OverwriteDocumentModelCreater.ConvertContents(contents);
            Assert.Equal(3, contentsMetadata.Count);
            Assert.Equal("summary,return,function", ExtractDictionaryKeys(contentsMetadata));
            Assert.Equal(2, ((Dictionary<string, object>) contentsMetadata["return"]).Count);
            Assert.Equal("description,type",
                ExtractDictionaryKeys((Dictionary<string, object>) contentsMetadata["return"]));
            Assert.Single((Dictionary<string, object>) contentsMetadata["function"]);
            Assert.Equal(2,
                ((List<Dictionary<string, object>>) ((Dictionary<string, object>) contentsMetadata["function"])["parameters"]).Count);
            Assert.Equal("id,description,type",
                ExtractDictionaryKeys(
                    ((List<Dictionary<string, object>>) ((Dictionary<string, object>) contentsMetadata["function"])["parameters"])[0]));
            Assert.Equal("id,description",
                ExtractDictionaryKeys(
                    ((List<Dictionary<string, object>>) ((Dictionary<string, object>) contentsMetadata["function"])["parameters"])[1]));
        }

        [Fact]
        public void DuplicateTest()
        {
            var testOPath = "function/parameters";
            var contents = new List<MarkdownPropertyModel>();

            contents.Add(new MarkdownPropertyModel
            {
                PropertyName = testOPath,
                PropertyNameSource = Markdown.Parse($"## `{testOPath}`")[0],
                PropertyValue = Markdown.Parse("test1").ToList()
            });
            contents.Add(new MarkdownPropertyModel
            {
                PropertyName = testOPath,
                PropertyNameSource = Markdown.Parse($"## `{testOPath}`")[0],
                PropertyValue = Markdown.Parse("test2").ToList()
            });

            Dictionary<string, object> contentsMetadata;
            Logger.RegisterListener(_listener);
            try
            {
                using (new LoggerPhaseScope("overwrite_document_model_creater"))
                {
                    contentsMetadata = OverwriteDocumentModelCreater.ConvertContents(contents);
                }
            }
            finally
            {
                Logger.UnregisterListener(_listener);
            }

            var logs = _listener.Items;
            Assert.Equal(1, logs.Count);
            Assert.Equal(1, logs.Where(l => l.Code == WarningCodes.Overwrite.InvalidOPaths).Count());
            Assert.Equal(1, contentsMetadata.Count);
            Assert.Equal("test2",
                ((ParagraphBlock) ((List<Block>) ((Dictionary<string, object>) contentsMetadata["function"])["parameters"])[0]).Inline.FirstChild.ToString());
        }

        [Fact]
        public void InvalidOPathsTest1()
        {
            var testBlockList = Markdown.Parse("Test").ToList();
            string[] testOPaths =
            {
                "function/parameters/description",
                "function/parameters[id=\"para1\"]/type",
            };
            var contents = new List<MarkdownPropertyModel>();
            foreach (var item in testOPaths)
            {
                contents.Add(new MarkdownPropertyModel
                {
                    PropertyName = item,
                    PropertyNameSource = Markdown.Parse($"## `{item}`")[0],
                    PropertyValue = testBlockList
                });
            }

            var ex = Assert.Throws<MarkdownFragmentsException>(() =>
                OverwriteDocumentModelCreater.ConvertContents(contents));
            Assert.Equal(
                "OPath function/parameters[id=\"para1\"]/type can not be appended to existing object since there is already an OPath like function/parameters/...",
                ex.Message);
            Assert.Equal(0, ex.Position);
        }

        [Fact]
        public void InvalidOPathsTest2()
        {
            var testBlockList = Markdown.Parse("Test").ToList();
            string[] testOPaths =
            {
                "function/parameters[id=\"para1\"]/type",
                "function/parameters/description",
            };
            var contents = new List<MarkdownPropertyModel>();
            foreach (var item in testOPaths)
            {
                contents.Add(new MarkdownPropertyModel
                {
                    PropertyName = item,
                    PropertyNameSource = Markdown.Parse($"## `{item}`")[0],
                    PropertyValue = testBlockList
                });
            }

            var ex = Assert.Throws<MarkdownFragmentsException>(() =>
                OverwriteDocumentModelCreater.ConvertContents(contents));
            Assert.Equal(
                "OPath function/parameters/description can not be appended to existing object since there is already an OPath like function/parameters[xx=xx]/...",
                ex.Message);
            Assert.Equal(0, ex.Position);
        }

        private string ExtractDictionaryKeys(Dictionary<string, object> dict)
        {
            return dict.Keys.ToList().Aggregate((a, b) => a + "," + b);
        }
    }
}
