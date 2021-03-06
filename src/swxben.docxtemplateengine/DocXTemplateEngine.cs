﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;

namespace swxben.docxtemplateengine
{
    public class DocXTemplateEngine : IDocXTemplateEngine
    {
        const string DocumentXmlPath = @"word/document.xml";
        public const string TOKEN_START = "«";
        public const string TOKEN_END = "»";
        private DocxXmlHandling _docxXmlHandling;

        public void Process(string source, Stream destination, object data, DocxXmlHandling docxXmlHandling = DocxXmlHandling.Ignore)
        {
            var sourceData = File.ReadAllBytes(source);
            destination.Write(sourceData, 0, sourceData.Length);
            destination.Seek(0, SeekOrigin.Begin);
            using (var zipFile = new ZipFile(destination))
            {
                ProcessZip(data, zipFile);
            }
        }

        public void Process(string source, string destination, object data, DocxXmlHandling docxXmlHandling = DocxXmlHandling.Ignore)
        {
            if (File.Exists(destination)) File.Delete(destination);

            File.Copy(source, destination);

            using (var zipFile = new ZipFile(destination))
            {
                ProcessZip(data, zipFile);
            }
        }

        public void Process(Stream sourceStream, Stream destinationStream, object data, DocxXmlHandling docxXmlHandling = DocxXmlHandling.Ignore)
        {
            sourceStream.CopyTo(destinationStream);
            destinationStream.Seek(0, SeekOrigin.Begin);
            
            using (var zipFile = new ZipFile(destinationStream))
            {
                ProcessZip(data, zipFile);
            }
        }

        private static void ProcessZip(object data, ZipFile zipFile, DocxXmlHandling docxXmlHandling = DocxXmlHandling.Ignore)
        {
            zipFile.BeginUpdate();

            var document = "";
            var entry = zipFile.GetEntry(DocumentXmlPath);
            if (entry == null)
            {
                throw new Exception(string.Format("Can't find {0} in template zip", DocumentXmlPath));
            }

            using (var s = zipFile.GetInputStream(entry))
            using (var reader = new StreamReader(s, Encoding.UTF8))
            {
                document = reader.ReadToEnd();
            }

            var newDocument = ParseTemplate(document, data, docxXmlHandling);

            zipFile.Add(new StringStaticDataSource(newDocument), DocumentXmlPath, CompressionMethod.Deflated, true);

            zipFile.CommitUpdate();

        }

        class StringStaticDataSource : IStaticDataSource
        {
            readonly string _source;

            public StringStaticDataSource(string source)
            {
                _source = source;
            }

            public Stream GetSource()
            {
                return new MemoryStream(Encoding.UTF8.GetBytes(_source));
            }
        }

        public static string ParseTemplate(string document, object data, DocxXmlHandling docxXmlHandling = DocxXmlHandling.Ignore)
        {
            document = data.GetType().GetFields().Aggregate(document,
                (current, field) => ReplaceTemplateField(current, field.Name, field.GetValue(data), docxXmlHandling));
            document = data.GetType().GetProperties().Aggregate(document, 
                (current, property) => ReplaceTemplateField(current, property.Name, property.GetValue(data, null), docxXmlHandling));
            
            var dictionary = data as IDictionary<string, object>;

            if (dictionary != null)
            {
                document = dictionary.Keys.Aggregate(document, (current, key) => ReplaceTemplateField(current, key, dictionary[key], docxXmlHandling));
            }

            return document;
        }

        public static string ReplaceTemplateField(string document, string fieldName, object fieldValue, DocxXmlHandling docxXmlHandling = DocxXmlHandling.Ignore)
        {
            var stringFieldValue = fieldValue == null ? string.Empty : fieldValue.ToString();
            if (docxXmlHandling == DocxXmlHandling.AutoEscape)
            {
                return document.Replace(TOKEN_START + fieldName + TOKEN_END, System.Security.SecurityElement.Escape(stringFieldValue));
            }
            return document.Replace(TOKEN_START + fieldName + TOKEN_END, stringFieldValue);
        }
    }
}
