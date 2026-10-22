// Amplify Color - Advanced Color Grading for Unity
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using UnityEngine;

#if UNITY_5_6_OR_NEWER
[ImageEffectAllowedInSceneView]
#endif
[ImageEffectTransformsToLDR]
[ExecuteInEditMode]
[AddComponentMenu( "Image Effects/Amplify Color" )]
public sealed class AmplifyColorEffect : AmplifyColorBase
{
}
