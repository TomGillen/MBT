﻿using System.Collections.Generic;
using System.Collections.Specialized;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace MediaBrowser.Theater.Api
{
    /// <summary>
    /// A Serializable Dictionary
    /// </summary>
    /// <typeparam name="TKey">
    /// The Key Type
    /// </typeparam>
    /// <typeparam name="TValue">
    /// The Value Type
    /// </typeparam>
    [XmlRoot("dictionary")]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
    {
        #region IXmlSerializable Members

        /// <summary>
        /// Get the Schema
        /// </summary>
        /// <returns>
        /// Nothing. We don't use this.
        /// </returns>
        public XmlSchema GetSchema()
        {
            return null;
        }

        /// <summary>
        /// Deserialize some XML into a dictionary
        /// </summary>
        /// <param name="reader">
        /// The reader.
        /// </param>
        public void ReadXml(XmlReader reader)
        {
            var keySerializer = new XmlSerializer(typeof(TKey));
            var valueSerializer = new XmlSerializer(typeof(TValue));

            bool wasEmpty = reader.IsEmptyElement;
            reader.Read();

            if (wasEmpty)
                return;

            while (reader.NodeType != XmlNodeType.EndElement)
            {
                reader.ReadStartElement("item");

                reader.ReadStartElement("key");
                TKey key = (TKey)keySerializer.Deserialize(reader);
                reader.ReadEndElement();

                reader.ReadStartElement("value");
                TValue value;
                if (reader.Name.Contains("ArrayOfString"))
                {
                    var scSerializer = new XmlSerializer(typeof(StringCollection));
                    value = (TValue)scSerializer.Deserialize(reader);
                }
                else
                {
                    value = (TValue)valueSerializer.Deserialize(reader);
                }
                reader.ReadEndElement();

                Add(key, value);

                reader.ReadEndElement();
                reader.MoveToContent();
            }
            reader.ReadEndElement();
        }

        /// <summary>
        /// Write the Dictionary out to XML
        /// </summary>
        /// <param name="writer">
        /// The writer.
        /// </param>
        public void WriteXml(XmlWriter writer)
        {
            var keySerializer = new XmlSerializer(typeof(TKey));
            var valueSerializer = new XmlSerializer(typeof(TValue));

            foreach (TKey key in Keys)
            {
                writer.WriteStartElement("item");

                writer.WriteStartElement("key");
                keySerializer.Serialize(writer, key);
                writer.WriteEndElement();

                writer.WriteStartElement("value");
                TValue value = this[key];

                if (value.GetType() == typeof(StringCollection))
                {
                    var scSerializer = new XmlSerializer(typeof(StringCollection));
                    scSerializer.Serialize(writer, value);
                    writer.WriteEndElement();
                }
                else
                {
                    valueSerializer.Serialize(writer, value);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }
        }
        #endregion
    }
}
