﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.OpenApi.Any;

namespace Microsoft.PowerFx.Connectors
{
    internal class SwaggerJsonSchema : SwaggerJsonExtensions, ISwaggerSchema, IEquatable<SwaggerJsonSchema>, IEquatable<ISwaggerSchema>
    {
        private readonly JsonElement _schema;

        public static ISwaggerSchema New(JsonElement schema)
        {
            return schema.ValueKind != JsonValueKind.Object ? null : new SwaggerJsonSchema(schema);
        }

        private SwaggerJsonSchema(JsonElement schema)
            : base(schema)
        {
            _schema = schema;
        }

        public string Description => SafeGetString("description");

        public string Title => SafeGetString("title");

        public string Format => SafeGetString("format");

        public string Type => SafeGetString("type");

        public IOpenApiAny Default => throw new NotImplementedException();

        public ISet<string> Required
        {
            get
            {
                HashSet<string> hs = new HashSet<string>();

                if (_schema.ValueKind != JsonValueKind.Array)
                {
                    return hs;
                }

                foreach (JsonElement je in _schema.EnumerateArray())
                {
                    hs.Add(je.GetString());
                }

                return hs;
            }
        }

        // Not supported yet
        public ISwaggerSchema AdditionalProperties => null;

        public IDictionary<string, ISwaggerSchema> Properties
        {
            get
            {
                if (!_schema.TryGetProperty("properties", out JsonElement jprops))
                {
                    return new Dictionary<string, ISwaggerSchema>();
                }

                Dictionary<string, ISwaggerSchema> props = new Dictionary<string, ISwaggerSchema>();

                if (jprops.ValueKind != JsonValueKind.Object)
                {
                    return props;
                }

                foreach (JsonProperty prop in jprops.EnumerateObject())
                {
                    props.Add(prop.Name, new SwaggerJsonSchema(prop.Value));
                }

                return props;
            }
        }

        public ISwaggerSchema Items => throw new NotImplementedException();

        public IList<IOpenApiAny> Enum
        {
            // Not supported yet
            get => null;

            set => throw new NotImplementedException();
        }

        // Not supported yet
        public ISwaggerReference Reference => null;

        // Not supported yet
        public ISwaggerDiscriminator Discriminator => null;

        public ISet<string> ReferenceTo => DataType == "reference" && _schema.TryGetProperty("referenceTo", out JsonElement val) && val.ValueKind == JsonValueKind.Array
                                                ? new HashSet<string>(val.EnumerateArray().Select(je => je.GetString()))
                                                : null;

        public string RelationshipName => SafeGetString("relationshipName");

        public string DataType => SafeGetString("datatype");

        private string SafeGetString(string key)
        {
            return _schema.TryGetProperty(key, out JsonElement val) ? val.GetString() : null;
        }

        public bool Equals(ISwaggerSchema other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return SwaggerSchema.Equals(this, other);
        }

        public bool Equals(SwaggerJsonSchema obj) => Equals(obj as ISwaggerSchema);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (ReferenceEquals(obj, null))
            {
                return false;
            }

            if (obj is ISwaggerSchema sws)
            {
                return SwaggerSchema.Equals(this, sws);
            }

            return false;
        }

        public static bool operator ==(SwaggerJsonSchema left, SwaggerJsonSchema right)
        {
            if (ReferenceEquals(left, null) && ReferenceEquals(right, null))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(SwaggerJsonSchema left, SwaggerJsonSchema right) => !(left == right);

        public override int GetHashCode() => _schema.ToString().GetHashCode();
    }
}
