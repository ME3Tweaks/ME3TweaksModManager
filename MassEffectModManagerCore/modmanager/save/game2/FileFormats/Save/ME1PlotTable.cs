using System.Collections;
using System.Collections.Generic;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats.Save
{
    // 00BAE040
    public class ME1PlotTable : IUnrealSerializable
    {
        public BitArray BoolVariables; // +00
        public List<int> IntVariables; // +0C
        public List<float> FloatVariables; // +18

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.BoolVariables);
            stream.Serialize(ref this.IntVariables);
            stream.Serialize(ref this.FloatVariables);
        }

        public bool GetBoolVariable(int index)
        {
            if (index >= this.BoolVariables.Count)
            {
                return false;
            }

            return this.BoolVariables[index];
        }

        public void SetBoolVariable(int index, bool value)
        {
            if (index >= this.BoolVariables.Count)
            {
                this.BoolVariables.Length = index + 1;
            }

            this.BoolVariables[index] = value;
        }

        public int GetIntVariable(int index)
        {
            if (index >= this.IntVariables.Count)
            {
                return 0;
            }

            return this.IntVariables[index];
        }

        public void SetIntVariable(int index, int value)
        {
            while (this.IntVariables.Count <= index)
            {
                this.IntVariables.Add(0);
            }

            this.IntVariables[index] = value;
        }

        public float GetFloatVariable(int index)
        {
            if (index >= this.FloatVariables.Count)
            {
                return 0;
            }

            return this.FloatVariables[index];
        }

        public void SetFloatVariable(int index, float value)
        {
            while (this.FloatVariables.Count <= index)
            {
                this.FloatVariables.Add(0);
            }

            this.FloatVariables[index] = value;
        }
    }
}
