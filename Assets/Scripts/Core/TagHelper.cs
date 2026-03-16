using System.Linq;
using UnityEngine;

namespace KotORUnity.Core
{
    /// <summary>
    /// Safe wrappers around Unity's tag API.
    ///
    /// Unity throws UnityException (not returns null) when you assign or look up
    /// a tag that has not been registered in Project Settings → Tags & Layers.
    /// Use these helpers so scripts survive a missing tag definition instead of
    /// crashing on the first frame.
    /// </summary>
    public static class TagHelper
    {
        /// <summary>
        /// Assign a tag to <paramref name="go"/> only if the tag exists in the
        /// project registry.  Logs a one-time actionable warning otherwise.
        /// </summary>
        public static void TrySetTag(GameObject go, string tag)
        {
            try
            {
                go.tag = tag;
            }
            catch (UnityException)
            {
                Debug.LogWarning(
                    $"[TagHelper] Tag '{tag}' is not registered. " +
                    $"Open Edit \u2192 Project Settings \u2192 Tags & Layers, " +
                    $"click [+] under Tags and add '{tag}', then re-enter Play mode.",
                    go);
            }
        }

        /// <summary>
        /// Like <see cref="GameObject.FindWithTag"/> but returns <c>null</c>
        /// instead of throwing when the tag is not registered.
        /// </summary>
        public static GameObject FindWithTag(string tag)
        {
            try
            {
                return GameObject.FindWithTag(tag);
            }
            catch (UnityException)
            {
                Debug.LogWarning(
                    $"[TagHelper] Tag '{tag}' is not registered. " +
                    $"Open Edit \u2192 Project Settings \u2192 Tags & Layers and add '{tag}'.");
                return null;
            }
        }
    }
}
