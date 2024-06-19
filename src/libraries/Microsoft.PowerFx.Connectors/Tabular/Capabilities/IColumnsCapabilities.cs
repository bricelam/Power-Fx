﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.PowerFx.Connectors
{
    internal interface IColumnsCapabilities
    {
        void AddColumnCapability(string name, ColumnCapabilitiesBase capability);
    }
}
