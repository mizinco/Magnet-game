using UnityEngine;

/// <summary>
/// Inspector でフィールドを読み取り専用にするための属性。
/// （実体の Drawer は Editor/ReadOnlyDrawer.cs にある）
/// </summary>
public class ReadOnlyAttribute : PropertyAttribute { }
