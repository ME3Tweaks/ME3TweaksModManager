using MassEffectModManagerCore.modmanager.helpers;
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

            using FileStream sourceStream = new FileStream(sourcefile, FileMode.Open);
            using FileStream patchStream = new FileStream(patchfile, FileMode.Open);
            MemoryStream outStream = new MemoryStream();
            ApplyJPatch(sourceStream, patchStream, outStream);
            File.WriteAllBytes(@"C:\Users\Mgamerz\Desktop\jdiff-cs\Patch_SFXPawn_Banshee-patched-cs.pcc", outStream.ToArray());
        }
        private enum JojoOpcode
        {
            OPERATION_ESC = 0xA7, //Opening opcoede
            OPERATION_MOD = 0xA6, //Overwrite bytes
            OPERATION_INS = 0xA5, //Insert bytes
            OPERATION_DEL = 0xA4, //Delete bytes
            OPERATION_EQL = 0xA3, //copy data (equal)
            OPERATION_BKT = 0xA2  //backtrack from current position
        }
        public static void ApplyJPatch(Stream sourceData, Stream patchData, Stream outData)
        {
            //all positions should be at 0.
            sourceData.Position = 0;
            patchData.Position = 0;
            outData.Position = 0;

            int readChar;

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
                            //Overwrite data
                            Debug.WriteLine("Opcode MOD at 0x" + (patchData.Position - 1).ToString("X6"));
                            processModInsOpcode(sourceData, patchData, outData, true);
                            break;
                        case JojoOpcode.OPERATION_INS:
                            //Insert new data
                            Debug.WriteLine("Opcode INS at 0x" + (patchData.Position - 1).ToString("X6"));
                            processModInsOpcode(sourceData, patchData, outData, false);
                            break;
                        case JojoOpcode.OPERATION_EQL:
                            //Equal, move pointers forward
                            Debug.WriteLine("Opcode EQL at 0x" + (patchData.Position - 1).ToString("X6"));
                            processEqlBktOpcode(sourceData, patchData, outData, false);
                            break;
                        case JojoOpcode.OPERATION_BKT:
                            //Backtrace, move backwards
                            Debug.WriteLine("Opcode BKT at 0x" + (patchData.Position - 1).ToString("X6"));
                            processEqlBktOpcode(sourceData, patchData, outData, true);
                            break;
                        case JojoOpcode.OPERATION_DEL:
                            //Backtrace, move backwards
                            Debug.WriteLine("Opcode DEL at 0x" + (patchData.Position - 1).ToString("X6"));
                            processDel(sourceData, patchData, outData);
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

            if (sourceData.Position != sourceData.Length)
            {
                Debug.WriteLine($"Not at end of source data. Len: 0x{sourceData.Length:X6} Pos: 0x{sourceData.Position:X6}");
            }
            else
            {
                Debug.WriteLine("At end of source data. OK");
            }
        }

        private static void processDel(Stream sourceData, Stream patchData, Stream outData)
        {
            //Delete data from source. Essentially, skip the data.
            var len = getJLength(patchData);
            sourceData.Position += len;
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
        /// Modifies or Inserts a series of bytes. Specify MOD using modifyMode = true, so the source data pointer moves as each new byte is written in.
        /// </summary>
        /// <param name="sourceData"></param>
        /// <param name="patchData"></param>
        /// <param name="outData"></param>
        private static void processModInsOpcode(Stream sourceData, Stream patchData, Stream outData, bool modifyMode)
        {
            int readChar;
            while ((readChar = patchData.ReadByte()) != -1)
            {
                if (readChar != (int)JojoOpcode.OPERATION_ESC)
                {
                    //not escape byte, just write it
                    outData.WriteByte((byte)readChar);
                    if (modifyMode)
                    {
                        sourceData.Position++;
                    }
                    continue;
                }

                int nextChar = patchData.ReadByte();
                if (nextChar == (int)JojoOpcode.OPERATION_ESC)
                {
                    //its <esc><esc>.
                    outData.WriteByte((byte)nextChar);
                    if (modifyMode)
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
                    Debug.WriteLine($"Encountered unexpected esc sequence value: {nextChar:X2}");
                    outData.WriteByte((byte)readChar);
                    outData.WriteByte((byte)nextChar);

                    if (modifyMode)
                    {
                        sourceData.Position += 2;
                    }
                }
            }
        }

        /// <summary>
        /// Advances the pointers forward or backwards as the source and destination file data is assumed to be the same.
        /// Data is copied from source data if backwards is false.
        /// </summary>
        /// <param name="sourceData"></param>
        /// <param name="patchData"></param>
        /// <param name="context"></param>
        /// <param name="backwards">For BCT Backtrace</param>
        private static void processEqlBktOpcode(Stream sourceData, Stream patchData, Stream outData, bool backwards)
        {
            var length = getJLength(patchData);
            Debug.WriteLine("  Length: " + length + " bytes");
            if (backwards)
            {
                sourceData.Position -= length;
            }
            else
            {
                //copy
                sourceData.CopyToEx(outData, length);
            }
        }

        public static bool isOpcode(int code)
        {
            return code >= 0xA2 && code <= 0xA6;
        }
    }
}
