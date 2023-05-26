﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System.Globalization;

namespace Microsoft.PowerFx.Core.Localization
{
    // $$$ Dead code, except CurrentUILanguageName (which should be removed)
    internal static class CurrentLocaleInfo
    {
        public static string CurrentLocaleName { get; set; } = CultureInfo.InvariantCulture.Name;

        public static string CurrentUILanguageName { get; set; } = CultureInfo.InvariantCulture.Name;

        public static int CurrentLCID { get; set; } = CultureInfo.InvariantCulture.LCID;
    }
}
