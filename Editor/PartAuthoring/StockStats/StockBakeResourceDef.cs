#if REDUX
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats
{
    /// <summary>JSON mirror of a resource definition .bytes file. Either raw or recipe shaped.</summary>
    /// <remarks>
    /// Raw resources (Methane, Oxidizer, Hydrogen, Xenon, Monoprop, etc.) carry a
    /// <c>data.massPerUnit</c>. Recipes (Methalox, MethaneAir, XenonEC) carry
    /// <c>recipeData.ingredients[]</c> with per-ingredient mix ratios and no direct mass. The
    /// bake's density side-pass reads both shapes from the same folder and resolves recipes
    /// recursively against raw masses.
    /// </remarks>
    internal sealed class StockBakeResourceDef
    {
        [JsonProperty("isRecipe")] public bool IsRecipe;
        [JsonProperty("data")] public RawResourceMirror Data;
        [JsonProperty("recipeData")] public RecipeMirror RecipeData;
    }

    internal sealed class RawResourceMirror
    {
        public string name;
        public float massPerUnit;
    }

    internal sealed class RecipeMirror
    {
        public string name;
        public List<RecipeIngredientMirror> ingredients;
    }

    internal sealed class RecipeIngredientMirror
    {
        public string name;
        public float unitsPerRecipeUnit;
    }
}
#endif
