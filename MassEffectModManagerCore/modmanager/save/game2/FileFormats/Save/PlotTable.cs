using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace MassEffectModManagerCore.modmanager.save.game2.FileFormats.Save
{
    // 00BAE5B0
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class PlotTable : IUnrealSerializable
    {
        public BitArray BoolVariables; // +00
        public List<int> IntVariables; // +0C
        public List<float> FloatVariables; // +18
        public int QuestProgressCounter; // +24
        public List<PlotQuest> QuestProgress; // +28
        public List<int> QuestIDs; // +34
        public List<PlotCodex> CodexEntries; // +40
        public List<int> CodexIDs; // +4C

        public void Serialize(IUnrealStream stream)
        {
            stream.Serialize(ref this.BoolVariables);
            stream.Serialize(ref this.IntVariables);
            stream.Serialize(ref this.FloatVariables);
            stream.Serialize(ref this.QuestProgressCounter);
            stream.Serialize(ref this.QuestProgress);
            stream.Serialize(ref this.QuestIDs);
            stream.Serialize(ref this.CodexEntries);
            stream.Serialize(ref this.CodexIDs);
        }

        [DisplayName("Paragon Points")]
        public int _helper_ParagonPoints
        {
            get { return this.GetIntVariable(2); }
            set { this.SetIntVariable(2, value); }
        }

        [DisplayName("Renegade Points")]
        public int _helper_RenegadePoints
        {
            get { return this.GetIntVariable(3); }
            set { this.SetIntVariable(3, value); }
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
            if (index >= this.IntVariables.Count)
            {
                this.IntVariables.Capacity = index + 1;
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
            if (index >= this.IntVariables.Count)
            {
                this.IntVariables.Capacity = index + 1;
            }

            this.FloatVariables[index] = value;
        }
    }
}
