using BepInEx.Configuration;
using UnityEngine;

namespace ConfiguredYoutubeBoombox
{
    public class ConfigNumberClamper : AcceptableValueBase
    {
        public ConfigNumberClamper(int min, int max) : base(typeof(int))
        {
            Minimum = min;
            Maximum = max;
        }

        internal int Minimum { get; } = int.MinValue;
        internal int Maximum { get; } = int.MaxValue;

        public override object Clamp(object value)
        {
            return Mathf.Clamp((int)value, Minimum, Maximum);
        }

        public override bool IsValid(object value)
        {
            var val = (int)value;

            return val >= Minimum && val <= Maximum;
        }

        public override string ToDescriptionString()
        {
            return $"# Range: [{Minimum}, {Maximum}]";
        }
    }
}