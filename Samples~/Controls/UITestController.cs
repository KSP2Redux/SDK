using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;

internal class UITestController : MonoBehaviour
{
    [SerializeField] private VisualTreeAsset testUxml;

    private void Start()
    {
        var options = WindowOptions.Default;
        options.IsHidingEnabled = false;
        Window.Create(
            options,
            testUxml
        );
    }
}