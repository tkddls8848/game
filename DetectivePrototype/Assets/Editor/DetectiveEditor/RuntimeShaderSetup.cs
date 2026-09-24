using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DetectiveEditor
{
    /// <summary>
    /// 빌드에 반드시 포함시켜야 하는 셰이더를 프로젝트 설정에 등록한다.
    ///
    /// 이 프로젝트는 UI를 에셋이 아니라 코드로 만든다(UIFactory 가 실행 중에 Image·Text 를 붙이고,
    /// WorldLabel 이 TextMesh 를 만든다). 그래서 씬에는 그 셰이더들을 참조하는 오브젝트가 하나도 없고,
    /// 빌드는 "아무도 안 쓰는 셰이더"로 보고 빼 버린다. 그 결과 플레이어에서 UI가 전부 마젠타로 나온다.
    /// 에디터에서는 모든 셰이더가 메모리에 있어 멀쩡해 보이므로, Play 로는 절대 안 잡히는 종류의 버그다.
    ///
    /// Graphics 설정의 Always Included Shaders 에 넣어 두면 참조가 없어도 빌드에 들어간다.
    /// 씬을 다시 만들 때마다 SceneBuilder 가 이걸 부른다.
    /// </summary>
    public static class RuntimeShaderSetup
    {
        /// <summary>
        /// 런타임에 코드로만 쓰이는 셰이더들. 전부 unity_builtin_extra 에 있어 강제 포함이 가능하다.
        ///
        /// TextMesh(WorldLabel)가 쓰는 "GUI/Text Shader" 는 일부러 넣지 않는다.
        /// 그건 Library/unity default resources 에 있는 엔진 내부 리소스라 HideFlags.DontSave 가 걸려 있고,
        /// 여기 등록하면 "An asset is marked with HideFlags.DontSave but is included in the build" 로
        /// 빌드가 실패한다. 어차피 그 묶음은 모든 플레이어에 항상 들어가므로 등록할 필요가 없다.
        /// </summary>
        public static readonly string[] RequiredShaders =
        {
            "UI/Default",      // uGUI Image·Text — UIFactory 가 실행 중에 붙인다
            "UI/Default Font", // 레거시 Text 폰트
            "Sprites/Default", // SpriteRenderer. 지금은 씬이 참조해 살아남지만 씬이 바뀌면 같이 사라진다
            "Sprites/Mask",
        };

        private const string SettingsPath = "ProjectSettings/GraphicsSettings.asset";
        private const string AlwaysIncludedProperty = "m_AlwaysIncludedShaders";

        /// <summary>엔진 내부 리소스 묶음. 여기 든 셰이더는 등록하면 빌드가 깨진다.</summary>
        private const string InternalResourcesPath = "Library/unity default resources";

        [MenuItem("Tools/Detective/Fix Always Included Shaders")]
        public static void EnsureFromMenu()
        {
            int changed = Ensure();
            Debug.Log(changed == 0
                ? "[RuntimeShaderSetup] 필요한 셰이더가 이미 전부 등록돼 있다."
                : "[RuntimeShaderSetup] Always Included Shaders를 " + changed + "건 고쳤다.");
        }

        /// <summary>
        /// 목록을 필요한 셰이더로 맞춘다. 빠진 것은 넣고, 빌드를 깨뜨리는 항목(엔진 내부 리소스)과
        /// 빈 칸은 걷어낸다. 고친 건수를 돌려주며, 이미 맞으면 0(설정 파일을 건드리지 않는다).
        /// </summary>
        public static int Ensure()
        {
            Object settings = GraphicsSettings.GetGraphicsSettings();
            if (settings == null)
            {
                Debug.LogWarning("[RuntimeShaderSetup] " + SettingsPath + " 을 읽지 못했다.");
                return 0;
            }

            var serialized = new SerializedObject(settings);
            SerializedProperty list = serialized.FindProperty(AlwaysIncludedProperty);
            if (list == null || !list.isArray)
            {
                Debug.LogWarning("[RuntimeShaderSetup] '" + AlwaysIncludedProperty + "' 배열을 찾지 못했다."
                    + " Unity 버전이 바뀌어 이름이 달라졌는지 확인할 것.");
                return 0;
            }

            int changed = 0;

            // 1) 빈 칸과 포함 불가능한 항목을 걷어낸다. 하나라도 있으면 빌드가 통째로 실패한다.
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var shader = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader != null && CanBeIncluded(shader)) continue;

                if (shader != null)
                    Debug.LogWarning("[RuntimeShaderSetup] '" + shader.name + "' 는 " + InternalResourcesPath
                        + " 의 내부 리소스라 빌드에 넣을 수 없다. 목록에서 뺀다(그 묶음은 어차피 항상 포함된다).");
                list.DeleteArrayElementAtIndex(i);
                changed++;
            }

            var present = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                Object existing = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing != null) present.Add(existing);
            }

            // 2) 빠진 것을 넣는다.
            for (int i = 0; i < RequiredShaders.Length; i++)
            {
                Shader shader = Shader.Find(RequiredShaders[i]);
                if (shader == null) continue;              // 버전에 따라 없을 수 있다
                if (!CanBeIncluded(shader)) continue;
                if (present.Contains(shader)) continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                present.Add(shader);
                changed++;
            }

            if (changed > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
            return changed;
        }

        /// <summary>
        /// Library/unity default resources 에 든 셰이더는 HideFlags.DontSave 라서 등록하면
        /// "An asset is marked with HideFlags.DontSave but is included in the build" 로 빌드가 실패한다.
        /// </summary>
        private static bool CanBeIncluded(Shader shader)
        {
            return AssetDatabase.GetAssetPath(shader) != InternalResourcesPath;
        }
    }
}
