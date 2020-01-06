using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{

    /// <summary>
    /// A C# Implemention of JoJoDiff's JPatch functionality
    /// </summary>
    public class JPatch
    {
        public static void DebugTest()
        {
            string sourcefile = @"C:\Users\Mgamerz\Desktop\jdiff-cs\Patch_SFXPawn_Banshee.pcc";
            string patchfile = @"C:\Users\Mgamerz\Desktop\jdiff-cs\bansheeattackspets.jsf";

            var outdata = File.ReadAllBytes(sourcefile).ToList();
            FileStream sourceStream = new FileStream(sourcefile, FileMode.Open);
            FileStream patchStream = new FileStream(patchfile, FileMode.Open);
            ApplyJPatch(sourceStream, patchStream, outdata);
        }
        private enum JojoOpcode
        {
            OPERATION_ESC = 0xa7, //Opening opcoede
            OPERATION_MOD = 0xa6, //Overwrite bytes
            OPERATION_INS = 0xa5, //Insert bytes
            OPERATION_DEL = 0xa4, //Delete bytes
            OPERATION_EQL = 0xa3, //?? Make data equal??
            OPERATION_BKT = 0xa2 //backtrack from current position
        }
        public static void ApplyJPatch(Stream sourceData, Stream patchData, List<byte> outData)
        {
            //all positions should be at 0.
            sourceData.Position = 0;
            patchData.Position = 0;

            PatchContext context = new PatchContext();
            context.outData = outData;

            int readChar;
            JojoOpcode currentOpcode;

            //Read patch data.
            while ((readChar = patchData.ReadByte()) != -1)
            {
                if (Enum.TryParse<JojoOpcode>(readChar.ToString(), out var opcode))
                {
                    //if (previousItemWasEsc && opcode == JojoOpcode.OPERATION_ESC)
                    //{
                    //    //it's esc esc, essentially escaping itself.
                    //    isEScEsc = true;
                    //}
                    //else
                    //{
                    //is opcode
                    //isEScEsc = false;
                    //currentOpcode = opcode;
                    //hasCurrentOpcode = true;
                    //}
                    switch (opcode)
                    {
                        case JojoOpcode.OPERATION_MOD:
                            Debug.WriteLine("Opcode MOD at 0x" + (patchData.Position - 1).ToString("X6"));
                            processModInsOpcode(sourceData, patchData, true, context);
                            break;
                        case JojoOpcode.OPERATION_INS:
                            Debug.WriteLine("Opcode INS at 0x" + (patchData.Position - 1).ToString("X6"));
                            processModInsOpcode(sourceData, patchData, false, context);
                            break;
                        case JojoOpcode.OPERATION_EQL:
                            Debug.WriteLine("Opcode EQL at 0x" + (patchData.Position - 1).ToString("X6"));
                            processEQL(sourceData, patchData, context);
                            break;
                        case JojoOpcode.OPERATION_ESC:
                            continue; //this is not actualy an opcode. SKip it.
                        default:
                            Debug.WriteLine("Unsupported opcode currently: " + opcode.ToString());
                            break;
                    }
                }
                else
                {
                    Debug.WriteLine("Invalid patch data. Found unexpected byte that is not an opcode: " + readChar.ToString("X2"));
                }
            }
        }

        private static int getJLength(Stream patchData)
        {
            /*From Jojo diff source documentation:
            * 
            *   <length> :   1 to 5 bytes for specifying a 32-bit unsigned number.
            *              1 <= x < 252        1 byte:   0-251
            *            252 <= x < 508        2 bytes:  252, x-252
            *            508 <= x < 0x10000    3 bytes:  253, xx
            *        0x10000 <= x < 0x100000000        5 bytes:  254, xxxx
            *                          9 bytes:  255, xxxxxxxx
            *                          
            */
            var byte1 = patchData.ReadByte();
            if (byte1 <= 251)
            {
                return byte1 + 1;
            }
            else if (byte1 == 252)
            {
                return byte1 + patchData.ReadByte() + 1;
            }
            else if (byte1 == 253)
            {
                return (patchData.ReadByte() << 8) + patchData.ReadByte();
            }
            else if (byte1 == 254)
            {
                return (patchData.ReadByte() << 24) + (patchData.ReadByte() << 16) + (patchData.ReadByte() << 8) + patchData.ReadByte();
            }
            else
            {
                Debug.WriteLine("Error! Unexpected byte when fetching length");
                return -1;
            }
        }

        /// <summary>
        /// Modifies or Inserts a series of bytes.
        /// </summary>
        /// <param name="sourceData"></param>
        /// <param name="patchData"></param>
        /// <param name="outData"></param>
        private static void processModInsOpcode(Stream sourceData, Stream patchData, bool advanceSourceDataPointer, PatchContext context)
        {
            int readChar;
            while ((readChar = patchData.ReadByte()) != -1)
            {
                if (readChar != (int)JojoOpcode.OPERATION_ESC)
                {
                    //not escape byte, just write it
                    context.OverwriteByte(readChar);
                    if (advanceSourceDataPointer)
                    {
                        sourceData.Position++;
                    }
                    continue;
                }

                int nextChar = patchData.ReadByte();
                if (nextChar == (int)JojoOpcode.OPERATION_ESC)
                {
                    //its <esc><esc>.
                    context.OverwriteByte(nextChar);
                    if (advanceSourceDataPointer)
                    {
                        sourceData.Position++;
                    }
                }
                else if (isOpcode(nextChar))
                {
                    //its <esc> <opcode>
                    //Roll back 2 bytes we read
                    patchData.Position -= 2;
                    return;
                }
                else
                {
                    //it's <esc>... nothing... this shouldn't be possible but maybe some sort of edge case.
                    context.OverwriteByte(readChar);
                    context.OverwriteByte(nextChar);
                    if (advanceSourceDataPointer)
                    {
                        sourceData.Position += 2;
                    }
                }
            }
        }

        /// <summary>
        /// Advances the pointers forward as the source and destination file data is assumed to be the same.
        /// This does NOT check the data is the same! JojoPatch did not seem to do this.
        /// </summary>
        /// <param name="sourceData"></param>
        /// <param name="patchData"></param>
        /// <param name="context"></param>
        private static void processEQL(Stream sourceData, Stream patchData, PatchContext context)
        {
            var equalLength = getJLength(patchData);
            Debug.WriteLine("  Data is same for length " + equalLength + " bytes");
            sourceData.Position += equalLength;
            context.outFilePosition += equalLength;
        }


        public static bool isOpcode(int code)
        {
            return code >= 0xA2 && code <= 0xA6;
        }

        private class PatchContext
        {
            public List<byte> outData;
            public int outFilePosition;

            /// <summary>
            /// Overwrites the byte at the current outFilePosition.
            /// </summary>
            /// <param name="readChar"></param>
            internal void OverwriteByte(int readChar)
            {
                if (outFilePosition >= outData.Count)
                {
                    throw new Exception("JPatch error: Attempting to overwrite a byte outside of outdata size.");
                }
                outData[outFilePosition] = (byte)readChar;
                outFilePosition++;
            }
        }
    }
}
