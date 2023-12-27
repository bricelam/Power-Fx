﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Net.Http;
using SharpYaml.Serialization.Logging;

namespace Microsoft.PowerFx.Connectors
{
    public abstract class BaseRuntimeConnectorContext
    {
        public abstract HttpMessageInvoker GetInvoker(string @namespace);

        public abstract TimeZoneInfo TimeZoneInfo { get; }

        public virtual bool ThrowOnError { get; } = false;

        internal virtual bool ReturnRawResults { get; } = false;

        internal virtual MediaKind MediaKind { get; set; } = MediaKind.NotBinary;

        public virtual ConnectorLogger ExecutionLogger { get; } = null; 

        internal BaseRuntimeConnectorContext WithRawResults(MediaKind mk)
        {
            return new RuntimeConnectorContextWithRawResults(this) { MediaKind = mk };
        }
    }

    public static class RuntimeConnectorContextExtensions
    {
        public static BasicServiceProvider AddRuntimeContext(this BasicServiceProvider serviceProvider, BaseRuntimeConnectorContext context)
        {
            return serviceProvider.AddService(typeof(BaseRuntimeConnectorContext), context);
        }
    }
}
