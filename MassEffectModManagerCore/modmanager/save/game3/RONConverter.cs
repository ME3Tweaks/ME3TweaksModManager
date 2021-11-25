using System.Collections.Generic;
using System.IO;

namespace MassEffectModManagerCore.modmanager.save.game3
{
    /// <summary>
    /// Converter for Trilogy Save Editor .ron (rust object notation)
    /// </summary>
    class RONConverter
    {
        public static MorphHead ConvertRON(string ronFilePath)
        {
            var lines = File.ReadAllLines(ronFilePath);
            var sectionIDs = new[]
            {
                @"accessory_mesh", @"morph_features", @"offset_bones", @"lod0_vertices", @"lod1_vertices",
                @"lod2_vertices",
                @"lod3_vertices", @"scalar_parameters", @"vector_parameters", @"texture_parameters"
            };

            var head = new MorphHead();
            string parsingSection = null;
            for (int i = 0; i < lines.Length; i++)
            {
                if (i == 0 || i == lines.Length - 1)
                    continue; // trash
                var line = lines[i];
                var keyValSplit = line.Split(':');

                if (i == 1)
                {
                    head.HairMesh = keyValSplit[1].Trim().Trim(',', '"');
                    continue;
                }

                bool cont = false;
                foreach (var si in sectionIDs)
                {
                    if (line.Contains(si))
                    {
                        parsingSection = si;
                        cont = true;
                        break;
                    }
                }

                if (cont)
                    continue;

                switch (parsingSection)
                {
                    case "accessory_mesh":
                        {
                            // ?
                            head.AccessoryMeshes.Add(line.Trim().Trim(',', '"'));
                        }
                        break;
                    case "morph_features":
                        {
                            if (keyValSplit.Length != 2)
                                continue; // ignore line
                            var scalar = getKeyedScalar(keyValSplit);
                            head.MorphFeatures.Add(new MorphHead.MorphFeature()
                            { Feature = scalar.Key, Offset = scalar.Value });
                        }
                        break;
                    case "offset_bones":
                        {
                            while (!line.Contains("}"))
                            {
                                // Read 4 lines
                                var boneName = lines[i].Split(':')[0].Trim().Trim('"');
                                Vector v = new Vector
                                {
                                    X = getKeyedScalar(lines[i + 1].Split(':')).Value,
                                    Y = getKeyedScalar(lines[i + 2].Split(':')).Value,
                                    Z = getKeyedScalar(lines[i + 3].Split(':')).Value
                                };

                                head.OffsetBones.Add(new MorphHead.OffsetBone() { Name = boneName, Offset = v });

                                i += 5; //skip )
                                line = lines[i];
                            }
                        }
                        break;
                    case "lod0_vertices":
                        readVertices(head.Lod0Vertices, lines, ref i);
                        break;
                    case "lod1_vertices":
                        readVertices(head.Lod1Vertices, lines, ref i);
                        break;
                    case "lod2_vertices":
                        readVertices(head.Lod2Vertices, lines, ref i);
                        break;
                    case "lod3_vertices":
                        readVertices(head.Lod3Vertices, lines, ref i);
                        break;
                    case "scalar_parameters":
                        {
                            if (keyValSplit.Length != 2)
                                continue; // ignore line
                            var scalar = getKeyedScalar(keyValSplit);
                            head.ScalarParameters.Add(new MorphHead.ScalarParameter()
                            { Name = scalar.Key, Value = scalar.Value });
                        }
                        break;
                    case "vector_parameters":
                        {
                            if (keyValSplit.Length != 2)
                                continue; // ignore line
                            var vector = getKeyedVector(keyValSplit);
                            head.VectorParameters.Add(new MorphHead.VectorParameter()
                            { Name = vector.Key, Value = vector.Value });
                        }
                        break;
                    case "texture_parameters":
                        {
                            if (keyValSplit.Length != 2)
                                continue; // ignore line
                            var scalar = getKeyedString(keyValSplit);
                            head.TextureParameters.Add(new MorphHead.TextureParameter()
                            { Name = scalar.Key, Value = scalar.Value });
                        }
                        break;
                }
            }

            return head;
        }


        private static void readVertices(List<Vector> vertices, string[] lines, ref int i)
        {
            var line = lines[i];
            while (!line.Contains("]"))
            {
                vertices.Add(new Vector
                {
                    X = getKeyedScalar(lines[i + 1].Split(':')).Value,
                    Y = getKeyedScalar(lines[i + 2].Split(':')).Value,
                    Z = getKeyedScalar(lines[i + 3].Split(':')).Value
                });

                i += 5; //skip )
                line = lines[i];
            }
        }

        private static KeyValuePair<string, float> getKeyedScalar(string[] keyValSplit)
        {
            var fn = keyValSplit[0].Trim().Trim('"');
            var off = float.Parse(keyValSplit[1].Trim().Trim(','));
            return new KeyValuePair<string, float>(fn, off);
        }

        private static KeyValuePair<string, LinearColor> getKeyedVector(string[] keyValSplit)
        {
            var fn = keyValSplit[0].Trim().Trim('"');
            var vectStr = keyValSplit[1].Trim().Trim('(', ')', ',').Split(',');
            return new KeyValuePair<string, LinearColor>(fn, new LinearColor()
            {
                R = float.Parse(vectStr[0]),
                G = float.Parse(vectStr[1]),
                B = float.Parse(vectStr[2]),
                A = float.Parse(vectStr[3]),
            });
        }

        private static KeyValuePair<string, string> getKeyedString(string[] keyValSplit)
        {
            var fn = keyValSplit[0].Trim().Trim('"');
            var off = keyValSplit[1].Trim().Trim(',');
            return new KeyValuePair<string, string>(fn, off);
        }
    }
}