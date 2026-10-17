using System;

// Original HLUnityUI.Runtime 02000008; abstract declarations from the 1.10.1 metadata, no bodies.
public interface IContentFrameControllerOverrideHandler
{
	event Action<bool> OnApply;

	bool IsOverrideActive();
}
