﻿using Microsoft.Xna.Framework;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using Barotrauma.Extensions;

namespace Barotrauma
{
    abstract class EditableParams : ISerializableEntity
    {
        public bool IsLoaded { get; protected set; }
        public string Name { get; private set; }
        public string FileName { get; private set; }
        public string Folder { get; private set; }
        public string FullPath { get; private set; }
        public Dictionary<string, SerializableProperty> SerializableProperties { get; protected set; }

        protected XDocument doc;
        public XDocument Doc
        {
            get
            {
                if (!IsLoaded)
                {
                    DebugConsole.ThrowError("[Params] Not loaded!");
                    return new XDocument();
                }
                return doc;
            }
            protected set
            {
                doc = value;
            }
        }

        protected virtual bool Deserialize(XElement element)
        {
            SerializableProperties = SerializableProperty.DeserializeProperties(this, element);
            return SerializableProperties != null;
        }

        protected virtual bool Serialize(XElement element)
        {
            SerializableProperty.SerializeProperties(this, element, true);
            return true;
        }

        protected virtual bool Load(string file)
        {
            UpdatePath(file);
            doc = XMLExtensions.TryLoadXml(FullPath);
            if (doc == null) { return false; }
            IsLoaded = Deserialize(doc.Root);
            return IsLoaded;
        }

        protected virtual void UpdatePath(string fullPath)
        {
            FullPath = fullPath;
            Name = Path.GetFileNameWithoutExtension(FullPath);
            FileName = Path.GetFileName(FullPath);
            Folder = Path.GetDirectoryName(FullPath);
        }

        public virtual bool Save(string fileNameWithoutExtension = null, XmlWriterSettings settings = null)
        {
            if (!Directory.Exists(Folder))
            {
                Directory.CreateDirectory(Folder);
            }
            Serialize(Doc.Root);
            if (settings == null)
            {
                settings = new XmlWriterSettings
                {
                    Indent = true,
                    OmitXmlDeclaration = true,
                    NewLineOnAttributes = true
                };
            }
            if (fileNameWithoutExtension != null)
            {
                UpdatePath(Path.Combine(Folder, $"{fileNameWithoutExtension}.xml"));
            }
            using (var writer = XmlWriter.Create(FullPath, settings))
            {
                Doc.WriteTo(writer);
                writer.Flush();
            }
            return true;
        }

        public virtual bool Reset()
        {
            return Deserialize(Doc.Root);
        }

#if CLIENT
        public SerializableEntityEditor SerializableEntityEditor { get; protected set; }
        public virtual void AddToEditor(ParamsEditor editor)
        {
            if (!IsLoaded)
            {
                DebugConsole.ThrowError("[Params] Not loaded!");
                return;
            }
            SerializableEntityEditor = new SerializableEntityEditor(editor.EditorBox.Content.RectTransform, this, false, true);
        }
#endif
    }
}
