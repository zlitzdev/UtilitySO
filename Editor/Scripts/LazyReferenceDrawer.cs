using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Zlitz.General.Registries
{
    [CustomPropertyDrawer(typeof(RegisterableObject<,,>.LazyReference))]
    internal sealed class LazyReferenceDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            PropertyField idField = new PropertyField(property.FindPropertyRelative("m_id"));
            idField.label = property.displayName;
            return idField;
        }
    }
}
