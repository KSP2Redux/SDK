using System.Linq;
using System.Text;

namespace ksp2community.ksp2unitytools.editor.Editor.Extensions
{
    public static class StringExtensions
    {
        public static string PascalToInspectorCase(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            var builder = new StringBuilder();
            builder.Append(char.ToUpper(str.First()));
            var appendedSpaceForPrevious = false;
            for (var i = 1; i < str.Length; i++)
            {
                if (char.IsUpper(str[i]) && i < str.Length - 1 && char.IsLower(str[i + 1]) && !appendedSpaceForPrevious)
                {
                    builder.Append(' ');
                }
                builder.Append(str[i]);
                appendedSpaceForPrevious = false;
                if (!char.IsLower(str[i]) || i >= str.Length - 1 || !char.IsUpper(str[i + 1])) continue;
                builder.Append(' ');
                appendedSpaceForPrevious = true;
            }

            return builder.ToString();
        }
    }
}