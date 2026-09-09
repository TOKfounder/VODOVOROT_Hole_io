using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameController))]
public class GameControllerEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		EditorGUILayout.Space(12f);
		EditorGUILayout.LabelField("Тест тутора", EditorStyles.boldLabel);

		GameController controller = (GameController)target;
		using (new EditorGUI.DisabledScope(!Application.isPlaying))
		{
			if (GUILayout.Button("Сбросить тутор"))
				controller.EditorResetTutorial();
			if (GUILayout.Button("Показать Mobile Canvas"))
				controller.EditorShowMobileCanvas();
			if (GUILayout.Button("Показать Desktop Canvas"))
				controller.EditorShowDesktopCanvas();
		}

		if (!Application.isPlaying)
			EditorGUILayout.HelpBox("Включите Play Mode, чтобы сбросить тутор и переключать Canvas.", MessageType.Info);
	}
}
