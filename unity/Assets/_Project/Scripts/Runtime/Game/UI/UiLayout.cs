using System;
using UnityEngine;

namespace SevenSteps.Game.UI
{
    /// <summary>Rules of docs/architecture.md 14 that one element may skip (<see cref="UiLayout.exempt"/>).</summary>
    [Flags]
    public enum UiLayoutRule { None = 0, Overlap = 1, Decoration = 2, Overflow = 4, Plate = 8, Contrast = 16, Board = 32, Safe = 64 }

    /// <summary>
    /// Layout marks read by UiLayoutCheck (docs/architecture.md 14.8): <see cref="secondary"/> text (minimum size 18
    /// instead of 22) and the rules this one element skips, with the reason. Not inherited by children. The minimum
    /// size 18 and the modal scrim (14.7) cannot be skipped. Code-built UI adds it with <see cref="Ui.Secondary"/> /
    /// <see cref="Ui.Exempt"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiLayout : MonoBehaviour
    {
        [Tooltip("14.2 secondary text: star counts, units, control help, credits body, status labels (minimum 18)")]
        public bool secondary;
        [Tooltip("Rules skipped for this element only (not its children)")]
        public UiLayoutRule exempt;
        [Tooltip("Required when exempt is not None (UiLayoutCheck reports an empty reason)")]
        public string reason = "";
    }

    public static partial class Ui
    {
        /// Marks <paramref name="text"/> as 14.2 secondary text (minimum size 18). Returns it for chaining.
        public static T Secondary<T>(T text) where T : Component
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            Mark(text.gameObject).secondary = true;
            return text;
        }

        /// Lets <paramref name="go"/> skip <paramref name="rules"/> (14.8); the reason is shown by UiLayoutCheck.
        public static GameObject Exempt(GameObject go, UiLayoutRule rules, string reason)
        {
            if (go == null) throw new ArgumentNullException(nameof(go));
            if (rules == UiLayoutRule.None) throw new ArgumentException("no rule to skip", nameof(rules));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("an exemption needs a reason (docs/architecture.md 14.8)", nameof(reason));
            var mark = Mark(go);
            mark.exempt |= rules;
            mark.reason = string.IsNullOrEmpty(mark.reason) ? reason : mark.reason + "; " + reason;
            return go;
        }

        static UiLayout Mark(GameObject go) => go.TryGetComponent<UiLayout>(out var m) ? m : go.AddComponent<UiLayout>();
    }
}
