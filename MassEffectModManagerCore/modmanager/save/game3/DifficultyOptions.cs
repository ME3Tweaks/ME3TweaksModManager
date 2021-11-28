/* Copyright (c) 2012 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

// ReSharper disable RedundantUsingDirective


// ReSharper restore RedundantUsingDirective

namespace ME3TweaksModManager.modmanager.save.game3
{
    [OriginalName("EDifficultyOptions")]
    public enum DifficultyOptions : byte
    {
        [OriginalName("DO_Level1")]
        Narritve = 0,

        [OriginalName("DO_Level2")]
        Casual = 1,

        [OriginalName("DO_Level3")]
        Normal = 2,

        [OriginalName("DO_Level4")]
        Hardcore = 3,

        [OriginalName("DO_Level5")]
        Insanity = 4,

        [OriginalName("DO_Level6")]
        //[DisplayName("What is beyond Insanity?")]
        //[Description("(it is a mystery)")]
        WhatIsBeyondInsanity = 5,
    }
}
