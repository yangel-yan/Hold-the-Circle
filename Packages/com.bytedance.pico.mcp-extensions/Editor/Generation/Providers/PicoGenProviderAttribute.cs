/*******************************************************************************
Copyright © 2015-2022 PICO Technology Co., Ltd.All rights reserved.

NOTICE：All information contained herein is, and remains the property of
PICO Technology Co., Ltd. The intellectual and technical concepts
contained herein are proprietary to PICO Technology Co., Ltd. and may be
covered by patents, patents in process, and are protected by trade secret or
copyright law. Dissemination of this information or reproduction of this
material is strictly forbidden unless prior written permission is obtained from
PICO Technology Co., Ltd.
*******************************************************************************/

// PicoGenProviderAttribute.cs
// Marks a class as a discoverable generation provider. The registry uses Unity's
// TypeCache to find every [PicoGenProvider] at editor load — the same zero-
// registration discovery pattern the MCP tool registry itself uses.

using System;

namespace ByteDance.PICO.Generation.Editor
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PicoGenProviderAttribute : Attribute
    {
        // Lower value = higher priority when multiple providers claim a modality.
        public int Priority { get; set; } = 100;
    }
}
