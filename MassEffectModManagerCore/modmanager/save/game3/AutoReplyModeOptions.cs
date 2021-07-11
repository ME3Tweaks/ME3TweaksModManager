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

namespace MassEffectModManagerCore.modmanager.save.game3
{
    [OriginalName("EAutoReplyModeOptions")]
    public enum AutoReplyModeOptions : byte
    {
        [OriginalName("ARMO_All_Decisions")]
        //[DisplayName("All Decisions")]
        AllDecisions = 0,

        [OriginalName("ARMO_Major_Decisions")]
        //[DisplayName("Major Decisions")]
        MajorDecisions = 1,

        [OriginalName("ARMO_No_Decisions")]
        //[DisplayName("No Decisions")]
        NoDecisions = 2,
    }
}
