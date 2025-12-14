using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor
{
    public class PersistentDictionary : ScriptableObject
    {
        public List<string> Keys = new();
        public List<string> Values = new();

        public void OnEnable()
        {
            if (Keys.Count != Values.Count)
            {
                Keys.Clear();
                Values.Clear();
            }
        }

        public bool Contains(string key)
        {
            return Keys.Contains(key);
        }

        public void Remove(string key)
        {
            int index = IndexOf(key);
            if (index != -1)
            {
                Keys.RemoveAt(index);
                Values.RemoveAt(index);
            }

            EditorUtility.SetDirty(this);
        }

        private int IndexOf(string key)
        {
            return Keys.IndexOf(key);
        }

        public bool TryGetValue(string key, out string value)
        {
            int index = IndexOf(key);
            if (index == -1)
            {
                value = null;
                return false;
            }
            else
            {
                value = Values[index];
                return true;
            }
        }

        public string this[string key]
        {
            get
            {
                int index = IndexOf(key);
                if (index == -1)
                {
                    throw new KeyNotFoundException(key);
                }
                else
                {
                    return Values[index];
                }
            }
            set
            {
                EditorUtility.SetDirty(this);
                int index = IndexOf(key);
                if (index == -1)
                {
                    Keys.Add(key);
                    Values.Add(value);
                }
                else
                {
                    Values[index] = value;
                }
            }
        }
    }
}