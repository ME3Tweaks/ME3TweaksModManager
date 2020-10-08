//#define JPATCH_DEBUG
// Uncomment the above to print debug output, same as -v on the jdiff.exe program (0.8.4)

/*
    A C# Implementation of JoJoDiff's JPatch functionality.
    Supports 0.8.4 and below diff files.
    Copyright (C) 2019-2020 Mgamerz

    Ported from the original implementation by Joris Heirbaut:
    http://jojodiff.sourceforge.net/

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace MassEffectModManagerCore.modmanager.me3tweaks
{
    /// <summary>
 
    /// </summary>
    [Localizable(false)]
    public class JPatch
    {
#if JPATCH_DEBUG
        public static void DebugTest()
        {
            // For debugging    

            var rootpath = @"C:\Users\Mgamerz\source\repos\Mgamerz\BuildTools\ME3Tweaks\Generic\PatchBuilder";
            var sourcefile = Path.Combine(rootpath, "BioP_MPBrdg-orig.pcc");
            //string oldpatch = Path.Combine(rootpath, "oldpatch");
            string newpatch = Path.Combine(rootpath, "patch");

            var destFile = Path.Combine(rootpath, "BioP_MPBrdg.pcc");
            var destMd5 = Utilities.CalculateMD5(destFile);
            var destSize = new FileInfo(destFile).Length;
            using FileStream sourceStream = new FileStream(sourcefile, FileMode.Open);
            //using FileStream oldPatchStream = new FileStream(oldpatch, FileMode.Open);
            using FileStream newPatchStream = new FileStream(newpatch, FileMode.Open);
            MemoryStream outStream = new MemoryStream();
            //ApplyJPatch(sourceStream, oldPatchStream, outStream);
            //var oldCalcedMd5 = Utilities.CalculateMD5(outStream);
            //if (destMd5 != oldCalcedMd5)
            //{
            //    Debug.WriteLine("Old jpatch failed! Wrong MD5");
            //}

            sourceStream.Position = 0;
            outStream = new MemoryStream();
            var result = ApplyJPatch(sourceStream, newPatchStream, outStream);

            if (outStream.Length != destSize)
            {
                Debug.WriteLine($"Wrong new patch size! Should be {destSize} however we got {outStream.Length}");
            }

            var newCalcedMd5 = Utilities.CalculateMD5(outStream);
            if (destMd5 != newCalcedMd5)
            {
                Debug.WriteLine("New jpatch failed! Wrong MD5");
            }
        }
#endif

        /// <summary>
        /// Opcodes used by JoJoDiff
        /// </summary>
        private enum JojoOpcode
        {
            /// <summary>
            /// Opening opcode that precedes other opcodes (unless followed by another ESC, in which case it deduces to a single ESC)
            /// </summary>
            OPERATION_ESC = 0xA7,
            /// <summary>
            /// Overwrite bytes (by writing from patch and advancing source position with it)
            /// </summary>
            OPERATION_MOD = 0xA6,
            /// <summary>
            /// Insert new byte (by writing new bytes without advancing the source)
            /// </summary>
            OPERATION_INS = 0xA5,
            /// <summary>
            /// Delete bytes (by skipping it in the source)
            /// </summary>
            OPERATION_DEL = 0xA4,
            /// <summary>
            /// Copy bytes (by copying directly from source to output)
            /// </summary>
            OPERATION_EQL = 0xA3,
            /// <summary>
            /// Backtrace source (move the source position backwards)
            /// </summary>
            OPERATION_BKT = 0xA2
        }

        /// <summary>
        /// End of file (-1)
        /// </summary>
        private const int EOF = -1;

        /// <summary>
        /// Apply a JPatch to a stream and put the result in another stream.
        /// </summary>
        /// <param name="sourceData">The source stream to patch against</param>
        /// <param name="patchData">The patch file stream</param>
        /// <param name="outData">The resulting patch output</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool ApplyJPatch(Stream sourceData, Stream patchData, Stream outData)
        {
            //all positions should be at 0.
            sourceData.Position = 0;
            patchData.Position = 0;
            outData.Position = 0;

            int readByte = 0;
            int peekChar1;
            int peekChar2;

            //Read patch data.
            while (readByte != EOF) // I don't think this condition will occur unless patch is malformed
            {
                // Read operator from input, unless this has already been done
                if (readByte == 0)
                {
                    peekChar1 = patchData.ReadByte();
                    if (peekChar1 == EOF)
                        break; // We have hit the end of the file

                    if (peekChar1 == (int)JojoOpcode.OPERATION_ESC)
                    {
                        peekChar2 = patchData.ReadByte();
                        switch (peekChar2)
                        {
                            case (int)JojoOpcode.OPERATION_EQL:
                            case (int)JojoOpcode.OPERATION_DEL:
                            case (int)JojoOpcode.OPERATION_BKT:
                            case (int)JojoOpcode.OPERATION_MOD:
                            case (int)JojoOpcode.OPERATION_INS:
                                readByte = peekChar2;
                                peekChar2 = EOF;
                                peekChar1 = EOF;
                                break;
                            case EOF:
                                // FAILED, EOF
                                Debug.WriteLine("Unexpected end of patch file!");
                                return false;
                            default:
                                // ESC xxx or ESC ESC at the start of a sequence
                                // Resolve by double pending bytes: peekChar1 and peekChar2
                                readByte = (int)JojoOpcode.OPERATION_MOD;
                                break;
                        }
                    }
                    else
                    {
                        // If an ESC <opr> is missing, set default operator (gaining two bytes)
                        readByte = (int)JojoOpcode.OPERATION_MOD;
                        peekChar2 = EOF;
                    }
                }
                else
                {
                    peekChar1 = EOF; // only needed when switching between MOD and INS
                    peekChar2 = EOF;
                }

                //Perform operations
                switch (readByte)
                {
                    case (int)JojoOpcode.OPERATION_MOD:
                        //Overwrite data
                        readByte = processModInsOpcode(sourceData, patchData, outData, true, peekChar1, peekChar2);
                        break;
                    case (int)JojoOpcode.OPERATION_INS:
                        //Insert new data
                        readByte = processModInsOpcode(sourceData, patchData, outData, false, peekChar1, peekChar2);
                        break;
                    case (int)JojoOpcode.OPERATION_EQL:
                        //Equal, move pointers forward
                        processEqlBktOpcode(sourceData, patchData, outData, false);
                        readByte = 0;
                        break;
                    case (int)JojoOpcode.OPERATION_BKT:
                        //Backtrace, move backwards
                        processEqlBktOpcode(sourceData, patchData, outData, true);
                        readByte = 0;
                        break;
                    case (int)JojoOpcode.OPERATION_DEL:
                        //Delete, move the source data position forward without writing anything out
                        processDel(sourceData, patchData);
                        readByte = 0; // Makes next op read fully (ESC 0xA7)
                        break;
                    default:
                        Debug.WriteLine("Unsupported opcode: " + readByte.ToString("X2"));
                        break;
                }
            }
#if JPATCH_DEBUG
            Debug.WriteLine($"\t{sourceData.Position}\t{outData.Position}\tEOF");
#endif
            if (patchData.Position != patchData.Length)
            {
                Debug.WriteLine("didn't read until end of patch file!");
            }

            return true; //OK
        }

        /// <summary>
        /// Deletes data from the source stream by advancing the source data position without writing it to the output stream.
        /// </summary>
        /// <param name="sourceData">The sourcedata stream</param>
        /// <param name="patchData">The patchdata stream</param>
        private static void processDel(Stream sourceData, Stream patchData)
        {
            //Delete data from source. Essentially, skip the data.
            var len = getJLength(patchData);
#if JPATCH_DEBUG
            Debug.WriteLine($"\t{sourceData.Position}\t{outData.Position}\tDEL\t{len}");
#endif
            sourceData.Position += len;
        }

        /// <summary>
        /// Reads a length value from the patch stream
        /// </summary>
        /// <param name="patchData">The patch stream/param>
        /// <returns>A length value, up to 2GiB</returns>
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
            * Length will never be zero as that makes no sense, you would never advance
            * positions by 0 or read 0 bytes
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
                // LARGE FILE SUPPORT (0xFF) IS NOT SUPPORTED
                // IN THIS IMPLEMENTATION OF JPATCH
                Debug.WriteLine("64-bit length numbers are not supported by this implementation of JPatch");
                return -1;
            }
        }

        /// <summary>
        /// Modifies or Inserts a series of bytes. Specify MOD using modifyMode = true, so the source data pointer moves as each new byte is written in.
        /// </summary>
        /// <param name="sourceData">The sourcedata stream</param>
        /// <param name="patchData">The patchdata stream</param>
        /// <param name="outData">The output data stream</param>
        /// <param name="modifyMode">If this is a MOD opcode operation. Indicate false to operate in INS mode</param>
        /// <param name="peekChar1">The first peeked byte ahead of the opcode</param>
        /// <param name="peekChar2">The second peeked byte ahead of the opcode</param>
        /// <returns></returns>
        private static int processModInsOpcode(Stream sourceData, Stream patchData, Stream outData, bool modifyMode, int peekChar1, int peekChar2)
        {
            // First write pending bytes
            if (peekChar1 != EOF)
            {
                outData.WriteByte((byte)peekChar1);
                if (modifyMode)
                {
                    sourceData.Position++;
                }

                if (peekChar1 == (int)JojoOpcode.OPERATION_ESC && peekChar2 != (int)JojoOpcode.OPERATION_ESC)
                {
                    outData.WriteByte((byte)peekChar2);
                    if (modifyMode)
                    {
                        sourceData.Position++;
                    }
                }
            }

            int readChar;
            while ((readChar = patchData.ReadByte()) != EOF)
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
#if JPATCH_DEBUG
                    Debug.WriteLine($"\t{sourceStartPos}\t{outStartPos}\t{(modifyMode ? "MOD" : "INS")}\t{outData.Position - outStartPos}");
#endif
                    return nextChar; // loop func will process this as the next opcode
                }
                else
                {
                    //it's <esc>... nothing... this shouldn't be possible but maybe some sort of edge case.
                    outData.WriteByte((byte)readChar);
                    outData.WriteByte((byte)nextChar);

                    if (modifyMode)
                    {
                        sourceData.Position += 2;
                    }
                }
            }
#if JPATCH_DEBUG
            Debug.WriteLine($"\t{sourceData.Position}\t{outData.Position}\t{(modifyMode ? "MOD" : "INS")}\t{outData.Position - outStartPos}");
#endif
            return readChar;
        }

        /// <summary>
        /// Advances the pointers forward or backwards as the source and destination file data is assumed to be the same.
        /// Data is copied from source data if backwards is false.
        /// </summary>
        /// <param name="sourceData">The source file stream</param>
        /// <param name="patchData">The patch data stream</param>
        /// <param name="outData">The output file stream</param>
        /// <param name="backwards">If this is a backtrace (BKT) opcode. If false, this is an EQL opcode and sourcedata will be copied to the outstream for the read length</param>
        private static void processEqlBktOpcode(Stream sourceData, Stream patchData, Stream outData, bool backwards)
        {
            var length = getJLength(patchData);
#if JPATCH_DEBUG
            Debug.WriteLine($"\t{sourceData.Position}\t{outData.Position}\t{(backwards ? "BKT" : "EQL")}\t{length}");
#endif
            if (backwards)
            {
                sourceData.Position -= length;
            }
            else
            {
                //copy
                CopyToEx(sourceData, outData, length);
            }
        }

        public static bool isOpcode(int code)
        {
            return code >= 0xA2 && code <= 0xA6;
        }

        /// <summary>
        /// Copies the inputstream to the outputstream, for the specified amount of bytes
        /// </summary>
        /// <param name="input">Stream to copy from</param>
        /// <param name="output">Stream to copy to</param>
        /// <param name="bytes">The number of bytes to copy</param>
        public static void CopyToEx(Stream input, Stream output, int bytes)
        {
            var buffer = new byte[32768];
            int read;
            while (bytes > 0 &&
                   (read = input.Read(buffer, 0, Math.Min(buffer.Length, bytes))) > 0)
            {
                output.Write(buffer, 0, read);
                bytes -= read;
            }
        }
    }
}
