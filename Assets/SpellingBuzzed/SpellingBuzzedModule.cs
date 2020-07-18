using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellingBuzzedModule : MonoBehaviour
{
	public KMBombInfo Bomb;
	public KMBombModule Module;
	public KMAudio Audio;
	public TextAsset ValidWordsText;
	public TextAsset BadPuzzlesText;

	public GameObject displayText;
	public GameObject bacText;
	public GameObject bacLabel;
	public GameObject transitionLabel;
	public KMSelectable[] keypadButtons;
	public KMSelectable submitButton;
	public KMSelectable resetButton;
	public GameObject[] stageLeds;

	private SpellingBuzzedController controller;

	private static int _moduleIdCounter = 1;
	private int _moduleId;

	void Start()
	{
		_moduleId = _moduleIdCounter++;

		controller = new SpellingBuzzedController(Bomb, ValidWordsText, BadPuzzlesText, LogPrefix());

		for (int buttonIndex = 0; buttonIndex < keypadButtons.Length; buttonIndex++) {
			KMSelectable button = keypadButtons[buttonIndex];
			button.OnInteract += GetKeypadButtonPressHandler(buttonIndex, button);
		}
		submitButton.OnInteract += GetSubmitButtonPressHandler(submitButton);
		resetButton.OnInteract += GetResetButtonPressHandler(resetButton);

		SetKeypadButtonLetters();
		Render();
	}

	private KMSelectable.OnInteractHandler GetKeypadButtonPressHandler(int buttonIndex, KMSelectable button)
	{
		return delegate {
			button.AddInteractionPunch();
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

			controller.AddLetter(LetterForButton(buttonIndex));
			Render();
			return false;
		};
	}

	private KMSelectable.OnInteractHandler GetSubmitButtonPressHandler(KMSelectable button)
	{
		return delegate {
			button.AddInteractionPunch();
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

			if (controller.SubmitInput() == SpellingBuzzedController.SubmitResult.FAILURE) {
				Module.HandleStrike();
				Render();
			} else if (controller.state == SpellingBuzzedController.State.SOLVED) {
				Module.HandlePass();
				Render();
			} else if (controller.state == SpellingBuzzedController.State.TRANSITION) {
				StartCoroutine(TransitionAnimation());
			}
			
			return false;
		};
	}

	private IEnumerator TransitionAnimation()
	{
		// Light LED for passed stage
		Render();

		MeshRenderer[] otherDisplayMeshes = {
			displayText.GetComponent<MeshRenderer>(),
			bacText.GetComponent<MeshRenderer>(),
			bacLabel.GetComponent<MeshRenderer>(),
		};
		MeshRenderer transitionLabelMesh = transitionLabel.GetComponent<MeshRenderer>();

		foreach (MeshRenderer mesh in otherDisplayMeshes)
			mesh.enabled = false;

		yield return new WaitForSeconds(1f);
		transitionLabelMesh.enabled = true;
		yield return new WaitForSeconds(2f);
		transitionLabelMesh.enabled = false;

		foreach (MeshRenderer mesh in otherDisplayMeshes)
			mesh.enabled = true;

		controller.FinishTransition();
		Render();
	}

	private KMSelectable.OnInteractHandler GetResetButtonPressHandler(KMSelectable button)
	{
		return delegate {
			button.AddInteractionPunch();
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

			controller.ResetInput();

			Render();
			return false;
		};
	}

	private void SetKeypadButtonLetters()
	{
		for (int buttonIndex = 0; buttonIndex < keypadButtons.Length; buttonIndex++) {
			string letter = LetterForButton(buttonIndex).ToString();
			keypadButtons[buttonIndex].transform.Find("ButtonText").GetComponent<TextMesh>().text = letter;
		}
	}

	private char LetterForButton(int buttonIndex)
	{
		// Center button
		if (buttonIndex == 3)
			return controller.puzzle.centerLetter;
		// N and W buttons
		if (buttonIndex >= 0 && buttonIndex <= 2)
			return controller.puzzle.otherLetters[buttonIndex];
		// E and S buttons
		if (buttonIndex >= 4 && buttonIndex <= 6)
			return controller.puzzle.otherLetters[buttonIndex - 1];

		Debug.LogFormat("{0} FATAL: Unknown buttonIndex {1}!", LogPrefix(), buttonIndex);
		Module.HandleStrike();
		return '!';
	}

	private void Render()
	{
		// Render display
		switch (controller.state) {
		case SpellingBuzzedController.State.IN_STAGE:
			bool hasInput = controller.inputChars.Length > 0;

			string displayString = controller.inputChars.ToString();
			displayText.GetComponent<TextMesh>().text = displayString;
			displayText.GetComponent<MeshRenderer>().enabled = hasInput;

			string bacString = string.Format("0.{0:D3}", controller.CurrentBac());
			bacText.GetComponent<TextMesh>().text = bacString;
			bacText.GetComponent<MeshRenderer>().enabled = !hasInput;
			bacLabel.GetComponent<MeshRenderer>().enabled = !hasInput;

			break;

		case SpellingBuzzedController.State.SOLVED:
			displayText.GetComponent<TextMesh>().text = "";
			break;
		}

		// Render LEDs
		for (int ledIndex = 0; ledIndex < stageLeds.Length; ledIndex++) {
			GameObject led = stageLeds[ledIndex];
			bool isOn = (ledIndex + 1) < controller.currentStage;
			led.transform.Find("On").GetComponent<MeshRenderer>().enabled = isOn;
			led.transform.Find("Off").GetComponent<MeshRenderer>().enabled = !isOn;
		}
	}

	private string LogPrefix()
	{
		return string.Format("[Spelling Buzzed #{0}]", _moduleId);
	}

	void Update()
	{

	}
}
