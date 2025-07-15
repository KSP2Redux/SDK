using System;
using ksp2community.ksp2unitytools.editor.Editor.Modding;
using UnityEngine.UIElements;

namespace ksp2community.ksp2unitytools.editor
{
    public class DependencyController
    {
        public DependencyController(VisualElement element, ModDependency dep, Action<ModDependency> cb)
        {
            var id = element.Q<TextField>("DependencyID");
            id.value = dep.id;
            id.RegisterValueChangedCallback(evt => { dep.id = evt.newValue; });
            var min = element.Q<TextField>("DependencyMin");
            min.value = dep.min;
            min.RegisterValueChangedCallback(evt => { dep.min = evt.newValue; });
            var max = element.Q<TextField>("DependencyMax");
            max.value = dep.max;
            max.RegisterValueChangedCallback(evt => { dep.max = evt.newValue; });
            var remove = element.Q<Button>("Remove");
            remove.clicked += () => { cb.Invoke(dep); };
        }
    }
}