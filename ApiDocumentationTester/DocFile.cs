﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ApiDocumentationTester
{
    public class DocFile
    {

        public string DisplayName { get; private set; }

        public string FullPath { get; private set; }

        public DocType Type { get; set; }

        public string HtmlContent { get; private set; }

        private MarkdownDeep.Block[] Blocks { get; set; }

        private List<MarkdownDeep.Block> m_CodeBlocks = new List<MarkdownDeep.Block>();
        private Dictionary<string, ResourceDefinition> m_Resources = new Dictionary<string, ResourceDefinition>();
        private List<MethodDefinition> m_Requests = new List<MethodDefinition>();

        public DocFile(string basePath, string relativePath)
        {
            FullPath = Path.Combine(basePath, relativePath.Substring(1));
            DisplayName = relativePath;
            Type = DocType.Unknown;
        }

        /// <summary>
        /// Load details about what's defined in the file into the class
        /// </summary>
        public void Scan()
        {
            MarkdownDeep.Markdown md = new MarkdownDeep.Markdown();
            md.SafeMode = false;
            md.ExtraMode = true;
            
            using (StreamReader reader = File.OpenText(this.FullPath))
            {
                HtmlContent = md.Transform(reader.ReadToEnd());
            }

            Blocks = md.Blocks;
            
            // Scan through the blocks to find something interesting
            m_CodeBlocks.Clear();
            foreach (var block in Blocks)
            {
                //Console.WriteLine("Block: {0}: {1}", block.BlockType, block.Content);    
                switch (block.BlockType)
                {
                    case MarkdownDeep.BlockType.codeblock:
                    case MarkdownDeep.BlockType.html:
                        m_CodeBlocks.Add(block);
                        break;
                    default:
                        break;
                }
            }

            for (int i = 0; i < m_CodeBlocks.Count;)
            {
                var htmlComment = m_CodeBlocks[i];
                if (htmlComment.BlockType != MarkdownDeep.BlockType.html)
                {
                    i++;
                    continue;
                }

                var codeBlock = m_CodeBlocks[i + 1];

                try 
                {
                    ParseCodeBlock(htmlComment, codeBlock);
                } 
                catch (Exception)
                {
                    Console.WriteLine("Warning: file has an invalid format.");
                }
                i += 2;
            }

        }

        public void ParseCodeBlock(MarkdownDeep.Block metadata, MarkdownDeep.Block code)
        {
            if (metadata.BlockType != MarkdownDeep.BlockType.html)
                throw new ArgumentException("metadata block does not appear to be metadata");
            if (code.BlockType != MarkdownDeep.BlockType.codeblock)
                throw new ArgumentException("code block does not appear to be code");

            var metadataJsonString = metadata.Content.Substring(4, metadata.Content.Length - 9);
            var metadataObject = (Newtonsoft.Json.Linq.JContainer)Newtonsoft.Json.JsonConvert.DeserializeObject(metadataJsonString);

            var resourceType = (string)metadataObject["@odata.type"];
            var blockType = (string)metadataObject["blockType"];
            

            if (blockType == "resource")
            {
                m_Resources.Add(resourceType, new ResourceDefinition { OdataType = resourceType, JsonFormat = code.Content });
            }
            else if (blockType == "request")
            {
                // parameters
                var parameters = metadataObject["parameters"];
                string[] parameterNames = null;
                if (null != parameters)
                {
                    var query = from p in parameters
                                select (string)p;
                    parameterNames = query.ToArray();
                }

                m_Requests.Add(new MethodDefinition {
                    Request = code.Content, 
                    DisplayName = string.Format("{1} #{0}", m_Requests.Count, DisplayName),
                    Parameters = parameterNames
                });
            }
            else if (blockType == "response")
            {
                var responseType = (string)metadataObject["@odata.type"];
                var lastRequest = m_Requests.Last();
                lastRequest.Response = code.Content;
                lastRequest.ResponseType = responseType;
            }
        }

        public MarkdownDeep.Block[] CodeBlocks
        {
            get { return m_CodeBlocks.ToArray(); }
        }

        public IReadOnlyDictionary<string, ResourceDefinition> Resources
        {
            get { return m_Resources; }
        }

        public MethodDefinition[] Requests
        {
            get { return m_Requests.ToArray(); }
        }
    }

    public enum DocType
    {
        Unknown = 0,
        Resource,
        MethodRequest
    }
}
