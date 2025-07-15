using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JetBrains.Annotations;
using KSP.Game.Missions;
using KSP.Sim.ResourceSystem;
using UnityEngine;

namespace ksp2community.ksp2unitytools.editor.Missions
{
    [Serializable]
    public class MissionAction
    {
        public string actionAqn = "";
        // stringKeys also contains types
        public string[] keys;
        public string[] values;
        
        [CanBeNull]
        public string GetString(string key)
        {
            var index = keys.IndexOf(key);
            if (index == -1) return null;
            return values[index];
        }

        public object GetEnum(string key, Type enumType)
        {
            var index = keys.IndexOf(key);
            if (index == -1) return Enum.GetValues(enumType).GetValue(0);
            return Enum.Parse(enumType, values[index]);
        }

        public long GetInt(string key)
        {
            var index = keys.IndexOf(key);
            if (index == -1) return 0;
            return long.TryParse(values[index], out var result) ? result : 0;
        }

        public double GetFloat(string key)
        {
            var index = keys.IndexOf(key);
            if (index == -1) return 0;
            return double.TryParse(values[index], out var result) ? result : 0;
        }

        public bool GetBool(string key)
        {
            var index = keys.IndexOf(key);
            return index != -1 && bool.TryParse(values[index], out var result) && result;
        }


        public Type GetType(string key)
        {
            var index = keys.IndexOf(key);
            if (index == -1) return null;
            return Type.GetType(values[index]);
        }

        public Vector3 GetVector3(string key)
        {
            var index = keys.IndexOf(key);
            if (index == -1) return new Vector3();
            var v = values[index].Split(',').Select(x => float.TryParse(x.Trim(), out var y) ? y : 0).ToArray();
            return new Vector3(v[0],v[1],v[2]);
        }

        public List<WorkspaceSelectionData> workspaceSelectionData; // Only used in the one mission type where this is a thing
        public DialogEntries dialogEntries; // Only used in the one mission type where this is a thing


        // There is going to need to be some magic here
        // This is very specifically meant to be very generic
        public IMissionAction ToMissionAction()
        {
            var type = Type.GetType(actionAqn);
            if (type == null) return null;
            var instance = Activator.CreateInstance(type) as IMissionAction;
            foreach (var field in type.GetFields())
            {
                if (field.FieldType == typeof(List<WorkspaceSelectionData>))
                {
                    field.SetValue(instance, workspaceSelectionData);
                }
                else if (field.FieldType == typeof(DialogEntries))
                {
                    field.SetValue(instance, dialogEntries);
                }
                if (keys.IndexOf(field.Name) == -1) continue;
                if (field.FieldType == typeof(string))
                {
                    field.SetValue(instance, GetString(field.Name));
                }
                else if (field.FieldType == typeof(int))
                {
                    field.SetValue(instance, (int)GetInt(field.Name));
                }
                else if (field.FieldType == typeof(long))
                {
                    field.SetValue(instance, GetInt(field.Name));
                }
                else if (field.FieldType == typeof(float))
                {
                    field.SetValue(instance, (float)GetFloat(field.Name));
                }
                else if (field.FieldType == typeof(double))
                {
                    field.SetValue(instance, GetFloat(field.Name));
                }
                else if (field.FieldType == typeof(bool))
                {
                    field.SetValue(instance, GetBool(field.Name));
                }
                else if (field.FieldType == typeof(Vector3))
                {
                    field.SetValue(instance, GetVector3(field.Name));
                }
                else if (field.FieldType.IsEnum)
                {
                    field.SetValue(instance, GetEnum(field.Name, field.FieldType));
                }
                else if (field.FieldType == typeof(Type))
                {
                    field.SetValue(instance, GetType(field.Name));
                }
            }
            return instance;
        }
    }
}