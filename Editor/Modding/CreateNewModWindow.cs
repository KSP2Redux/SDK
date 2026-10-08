using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Modding
{
    public class CreateNewModWindow : EditorWindow
    {
        [MenuItem("Modding/Create New Mod")]
        public static void ShowWindow()
        {
            EditorWindow window = GetWindow<CreateNewModWindow>();
            window.titleContent = new GUIContent("Create New Mod");
        }

        private void CreateGUI()
        {
            TemplateContainer doc = AssetDatabase
                .LoadAssetAtPath<VisualTreeAsset>(
                    SDKConfiguration.BasePath + "/Assets/Windows/CreateNewModWindow.uxml"
                )
                .Instantiate();
            rootVisualElement.Add(doc);
            Ksp2UnityToolsStyles.Apply(rootVisualElement);

            var field = rootVisualElement.Q<TextField>("ModIdField");
            var addCode = rootVisualElement.Q<Toggle>("AddCodeToggle");
            var helpSlot = rootVisualElement.Q<VisualElement>("ModIdHelpSlot");
            var create = rootVisualElement.Q<Button>("Create");
            var cancel = rootVisualElement.Q<Button>("Cancel");

            var help = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            help.style.display = DisplayStyle.None;
            helpSlot.Add(help);

            void Refresh()
            {
                string id = (field.value ?? string.Empty).Normalize();
                if (string.IsNullOrEmpty(id))
                {
                    help.style.display = DisplayStyle.None;
                    create.SetEnabled(false);
                    return;
                }
                if (!IsValidIdentifier(id))
                {
                    help.text = "Mod ID is not a valid C# identifier.";
                    help.style.display = DisplayStyle.Flex;
                    create.SetEnabled(false);
                    return;
                }
                if (Directory.Exists($"Assets/{id}"))
                {
                    help.text = $"Folder 'Assets/{id}' already exists.";
                    help.style.display = DisplayStyle.Flex;
                    create.SetEnabled(false);
                    return;
                }
                help.style.display = DisplayStyle.None;
                create.SetEnabled(true);
            }

            field.RegisterValueChangedCallback(_ => Refresh());
            Refresh();

            create.clicked += () =>
            {
                CreateMod(field.value, addCode.value);
                Close();
            };
            cancel.clicked += Close;
        }

        private static void CreateMod(string id, bool addAssembly)
        {
            id = id.Normalize();
            Directory.CreateDirectory($"Assets/{id}");
            Directory.CreateDirectory($"Assets/{id}/Copied");
            Directory.CreateDirectory($"Assets/{id}/Copied/localizations");
            Directory.CreateDirectory($"Assets/{id}/Copied/Patches");
            Directory.CreateDirectory($"Assets/{id}/Definitions/Parts");
            Directory.CreateDirectory($"Assets/{id}/Definitions/Missions");
            Directory.CreateDirectory($"Assets/{id}/Definitions/Experiments");

            var info = CreateInstance<Mod>();
            info.id = id;
            info.name = id;
            info.SyncPickerDisplayName();
            AssetDatabase.CreateAsset(info, $"Assets/{id}/{id}.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (addAssembly)
            {
                info.CreateAssembly();
            }

            info.CreateAddressablesGroups();
            info.RefreshPipelines();
            Selection.activeObject = info;
        }

        private const string Start = @"(\p{Lu}|\p{Ll}|\p{Lt}|\p{Lm}|\p{Lo}|\p{Nl})";
        private const string Extend = @"(\p{Mn}|\p{Mc}|\p{Nd}|\p{Pc}|\p{Cf})";
        private static readonly Regex IdentRegex = new($"^{Start}({Start}|{Extend})*$", RegexOptions.Compiled);

        private static bool IsValidIdentifier(string modId)
        {
            return IdentRegex.IsMatch(modId);
        }
    }
}
