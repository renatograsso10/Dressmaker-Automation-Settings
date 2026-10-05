using System;
using System.Reflection;

namespace DressmakerAutomation.SettingsUI
{
    internal sealed class CoreBridge
    {
        private readonly FieldInfo money;
        private readonly FieldInfo floor;
        private readonly FieldInfo alignment;
        private readonly FieldInfo placement;
        private readonly FieldInfo mannequin;

        internal CoreBridge()
        {
            var type = Type.GetType("DressmakerAutomation.AutomationHooks, DressmakerAutomation.Core", true);
            money = Field(type, "_infiniteMoney", typeof(bool));
            floor = Field(type, "_goldFloor", typeof(int));
            alignment = Field(type, "_autoGrainAlignment", typeof(bool));
            placement = Field(type, "_findValidPosition", typeof(bool));
            mannequin = Field(type, "_autoMannequinSizing", typeof(bool));
        }

        private static FieldInfo Field(Type type, string name, Type expected)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null || field.FieldType != expected)
                throw new InvalidOperationException("Unsupported automation core: " + name);
            return field;
        }

        internal Settings Read()
        {
            return new Settings
            {
                InfiniteMoney = (bool)money.GetValue(null),
                GoldFloor = (int)floor.GetValue(null),
                AutoGrainAlignment = (bool)alignment.GetValue(null),
                FindValidPosition = (bool)placement.GetValue(null),
                AutoMannequinSizing = (bool)mannequin.GetValue(null)
            };
        }

        internal void Apply(Settings settings)
        {
            money.SetValue(null, settings.InfiniteMoney);
            floor.SetValue(null, settings.GoldFloor);
            alignment.SetValue(null, settings.AutoGrainAlignment);
            placement.SetValue(null, settings.FindValidPosition);
            mannequin.SetValue(null, settings.AutoMannequinSizing);
        }
    }
}
