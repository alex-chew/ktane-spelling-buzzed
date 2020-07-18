using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Rnd = UnityEngine.Random;
using UnityEngine;
using KModkit;

public class SpellingBuzzedController
{
	private const string VOWELS = "AUEOI";
	private const string CONSONANTS_COMMON = "TWNDSCRMLH";
	private const string CONSONANTS_RARE = "PZBXGVKJFY";

	private const int MAX_INPUT_CHARS = 8;
	private const int NUM_STAGES = 3;

	private static HashSet<string> validWords = null;
	private static HashSet<string> badPuzzles = null;

	private KMBombInfo bomb;
	private string logPrefix;

	public State state { get; private set; }

	public int currentStage { get; private set; }

	public Puzzle puzzle { get; private set; }

	public Shift[] shifts { get; private set; }

	public StringBuilder inputChars { get; private set; }

	public List<string> acceptedSubmissions;

	public class Puzzle
	{
		public string vowels { get; private set; }

		public string commonConsonants { get; private set; }

		public string rareConsonants { get; private set; }

		public string displayOrder { get; private set; }

		public Puzzle(string vowels, string commonConsonants, string rareConsonants)
		{
			// Vowel order is important: first is center letter
			this.vowels = vowels;

			// Sort the consonants, since it's required for normalization anyway
			this.commonConsonants = new string(commonConsonants.OrderBy(c => c).ToArray());
			this.rareConsonants = new string(rareConsonants.OrderBy(c => c).ToArray());

			// Shuffle all but the center letter, store as display order
			char[] displayChars = (vowels.Substring(1) + this.commonConsonants + this.rareConsonants).ToCharArray();
			Shuffle(displayChars);
			this.displayOrder = vowels.Substring(0, 1) + new string(displayChars);
		}

		public char centerLetter {
			get {
				return vowels[0];
			}
		}

		public string otherLetters {
			get {
				return displayOrder.Substring(1);
			}
		}

		public string Normalized()
		{
			return vowels + commonConsonants + rareConsonants;
		}

		public Puzzle Shifted(int offset)
		{
			string shiftedLetters = ShiftText(Normalized(), offset);
			string shiftedVowels = shiftedLetters.Substring(0, 2);
			string shiftedCommonConsonants = shiftedLetters.Substring(2, 4);
			string shiftedRareConsonants = shiftedLetters.Substring(6, 1);
			return new Puzzle(shiftedVowels, shiftedCommonConsonants, shiftedRareConsonants);
		}
	}

	public class Shift
	{
		// bacThousandths = 012 means BAC = 0.012%
		public int bacThousandths;
		// Number of spaces
		public int amount;
		// 1 is CW, -1 is CCW
		public int direction;
		public string shiftAmountExplanation;
		public string shiftDirectionExplanation;

		public int offset {
			get {
				return amount * direction;
			}
		}
	}

	public enum State
	{
		IN_STAGE,
		TRANSITION,
		SOLVED,
	}

	public SpellingBuzzedController(KMBombInfo bomb, TextAsset validWordsText, TextAsset badPuzzlesText, string logPrefix)
	{
		this.bomb = bomb;
		this.logPrefix = logPrefix;

		LoadValidWords(validWordsText);
		LoadBadPuzzles(badPuzzlesText);
		do {
			puzzle = SamplePuzzle();
			shifts = SampleShifts();
		} while (HasBadShift());

		state = State.IN_STAGE;
		currentStage = 1;
		inputChars = new StringBuilder();
		acceptedSubmissions = new List<string>();

		LogCurrentStage();
	}

	public void AddLetter(char letter)
	{
		if (inputChars.Length >= MAX_INPUT_CHARS)
			return;

		inputChars.Append(letter);
	}

	public enum SubmitResult
	{
		INVALID,
		SUCCESS,
		FAILURE,
	}

	public SubmitResult SubmitInput()
	{
		if (state != State.IN_STAGE)
			return SubmitResult.INVALID;

		string inputString = inputChars.ToString();
		if (inputString.Length < 4) {
			Debug.LogFormat("{0} Input '{1}' is wrong since it has less than 4 characters. Strike.",
				logPrefix, inputString);
			return SubmitResult.FAILURE;
		}
		if (!inputString.Contains(puzzle.centerLetter)) {
			Debug.LogFormat("{0} Input '{1}' is wrong since it doesn't contain the center letter '{2}'. Strike.",
				logPrefix, inputString, puzzle.centerLetter);
			return SubmitResult.FAILURE;
		}

		string submission = ShiftText(inputString, CurrentShiftOffset());
		Debug.LogFormat("{0} Input '{1}' becomes submission '{2}' after applying shift.",
			logPrefix, inputString, submission);

		if (acceptedSubmissions.Contains(submission)) {
			Debug.LogFormat("{0} Submission '{1}' has already been submitted before. Strike.",
				logPrefix, submission);
			return SubmitResult.FAILURE;
		}
		if (!validWords.Contains(submission)) {
			Debug.LogFormat("{0} Submission '{1}' is not a valid word. Strike.",
				logPrefix, submission);
			return SubmitResult.FAILURE;
		}

		acceptedSubmissions.Add(submission);
		Debug.LogFormat("{0} Submission '{1}' is correct for stage {2}.",
			logPrefix, submission, currentStage);
		inputChars.Remove(0, inputChars.Length);
		currentStage++;
		if (currentStage == NUM_STAGES + 1) {
			state = State.SOLVED;
			Debug.LogFormat("{0} Module solved.", logPrefix);
		} else {
			state = State.TRANSITION;
			LogCurrentStage();
		}
		return SubmitResult.SUCCESS;
	}

	public void ResetInput()
	{
		inputChars.Remove(0, inputChars.Length);
	}

	public void FinishTransition()
	{
		state = State.IN_STAGE;
	}

	public int CurrentBac()
	{
		return shifts[currentStage - 1].bacThousandths;
	}

	private void LogCurrentStage()
	{
		Debug.LogFormat("{0} Starting stage {1} of {2}.", logPrefix, currentStage, NUM_STAGES);

		Shift shift = shifts[currentStage - 1];
		Debug.LogFormat("{0} BAC is 0.{1:D3}%, letters are {2}.",
			logPrefix, shift.bacThousandths, ShiftText(puzzle.displayOrder, shift.offset)
		);
		Debug.LogFormat("{0} {1}", logPrefix, shift.shiftAmountExplanation);
		Debug.LogFormat("{0} {1}", logPrefix, shift.shiftDirectionExplanation);
	}

	private void LoadValidWords(TextAsset validWordsText)
	{
		if (validWords != null)
			return;

		validWords = new HashSet<string>(validWordsText.text.Split(new char[] { '\n' }).AsEnumerable());
		validWords.Remove("");
		Debug.LogFormat("{0} Loaded {1} valid words.", logPrefix, validWords.Count);
	}

	private void LoadBadPuzzles(TextAsset badPuzzlesText)
	{
		if (badPuzzles != null)
			return;

		badPuzzles = new HashSet<string>(badPuzzlesText.text.Split(new char[] { '\n' }).AsEnumerable());
		badPuzzles.Remove("");
		Debug.LogFormat("{0} Loaded {1} bad puzzles.", logPrefix, badPuzzles.Count);
	}

	private bool HasBadShift()
	{
		string[] shiftedPuzzleNorms = new string[] {
			puzzle.Shifted(shifts[0].offset).Normalized(),
			puzzle.Shifted(shifts[1].offset).Normalized(),
			puzzle.Shifted(shifts[2].offset).Normalized(),
		};
		return shiftedPuzzleNorms.Any(puzzleNorm => badPuzzles.Contains(puzzleNorm));
	}

	private Puzzle SamplePuzzle()
	{
		string vowels = ShuffledSubstring(VOWELS, 2);
		string commonConsonants = ShuffledSubstring(CONSONANTS_COMMON, 4);
		string rareConsonants = ShuffledSubstring(CONSONANTS_RARE, 1);
		Puzzle puzzle = new Puzzle(vowels, commonConsonants, rareConsonants);
		return puzzle;
	}

	private string ShuffledSubstring(string letters, int count)
	{
		char[] lettersArray = letters.ToCharArray();
		Shuffle(lettersArray);
		return new string(lettersArray).Substring(0, count);
	}

	private Shift[] SampleShifts()
	{
		int[] bacs = { Rnd.Range(1, 30), Rnd.Range(30, 80), Rnd.Range(80, 160) };
		Shift[] shifts = new Shift[NUM_STAGES];
		for (int shiftIndex = 0; shiftIndex < NUM_STAGES; shiftIndex++) {
			int bac = bacs[shiftIndex];
			int stage = shiftIndex + 1;
			string shiftAmountExplanation;
			string shiftDirectionExplanation;
			int amount = BacToShiftAmount(bac, stage, out shiftAmountExplanation);
			int direction = BacToShiftDirection(bac, stage, out shiftDirectionExplanation);
			shifts[shiftIndex] = new Shift {
				bacThousandths = bac,
				amount = amount,
				direction = direction,
				shiftAmountExplanation = shiftAmountExplanation,
				shiftDirectionExplanation = shiftDirectionExplanation,
			};
		}
		return shifts;
	}

	private int BacToShiftAmount(int bac, int stage, out string explanation)
	{
		int amount;
		if (bac < 20 && (bomb.IsIndicatorOn(Indicator.CLR) || bomb.IsIndicatorOff(Indicator.TRN))) {
			amount = bac % 2;
			explanation = string.Format(
				"{0} BAC is less than 0.020%, and lit CLR or unlit TRN indicator present, so shift amount is (BAC mod 2) == {1}",
				logPrefix, amount
			);
		} else if (bac < 50 && PuzzleLettersAtLeastAsDrunkAsSober()) {
			amount = bac % 3;
			explanation = string.Format(
				"{0} BAC is less than 0.050%, and puzzle letters intersect DRUNK at least as much as SOBER, so shift amount is (BAC mod 3) == {1}",
				logPrefix, amount
			);
		} else if (bac < 70 && bomb.GetBatteryCount() > stage - 1) {
			amount = bac % 5;
			explanation = string.Format(
				"{0} BAC is less than 0.070%, and there are more batteries than stages passed ({1}), so shift amount is (BAC mod 5) == {2}",
				logPrefix, stage - 1, amount
			);
		} else if (bac < 100 && IndicatorLettersIntersectCheers()) {
			amount = (bac % 5) + 2;
			explanation = string.Format(
				"{0} BAC is less than 0.100%, and some indicator has a letter in CHEERS, so shift amount is (BAC mod 5) + 2 = {1}",
				logPrefix, amount
			);
		} else {
			amount = (bac % 7) + 1;
			explanation = string.Format(
				"{0} No other rules apply, so shift amount is (BAC mod 7) + 1 = {1}",
				logPrefix, amount
			);
		}
		return amount;
	}

	private bool PuzzleLettersAtLeastAsDrunkAsSober()
	{
		List<char> puzzleLetters = new List<char>(puzzle.otherLetters);
		puzzleLetters.Add(puzzle.centerLetter);

		int drunk = "DRUNK".Intersect(puzzleLetters).Count();
		int sober = "SOBER".Intersect(puzzleLetters).Count();
		return drunk >= sober;
	}

	private bool IndicatorLettersIntersectCheers()
	{
		return bomb
			.GetIndicators()
			.Where(ind => ind.Where(letter => "CHEERS".Contains(letter)).Any())
			.Any();
	}

	private int BacToShiftDirection(int bac, int stage, out string explanation)
	{
		if (stage <= 2 && (bac % 2 == 0)) {
			explanation = string.Format(
				"{0} Stage {1} is at most 2, and BAC is even, so shift direction is CW",
				logPrefix, stage
			);
			return 1;
		}
		if ((puzzle.centerLetter == 'O' || puzzle.otherLetters.Contains('O')) && bac < 60) {
			explanation = string.Format(
				"{0} Keypad letters include 'O', and BAC is less than 0.060%, so shift direction is CCW",
				logPrefix
			);
			return -1;
		}
		if (bomb.GetSerialNumberNumbers().Contains(stage)) {
			explanation = string.Format(
				"{0} Serial number digits include stage {1}, so shift direction is CW",
				logPrefix, stage
			);
			return 1;
		}
		if (bac < 130 && bomb.GetPortPlateCount() < 3) {
			explanation = string.Format(
				"{0} BAC is less than 0.130%, and bomb has less than 3 port plates, so shift direction is CCW",
				logPrefix
			);
			return -1;
		}
		explanation = string.Format(
			"{0} No other shift direction rules apply, so shift direction is CW",
			logPrefix
		);
		return 1;
	}

	private static void Shuffle<T>(T[] values)
	{
		int n = values.Length;
		for (int i = 0; i < n - 1; i++) {
			int j = Rnd.Range(i, n);
			T tmp = values[i];
			values[i] = values[j];
			values[j] = tmp;
		}
	}

	private int CurrentShiftOffset()
	{
		return shifts[currentStage - 1].offset;
	}

	private static Dictionary<int, Dictionary<char, char>> translationCache = new Dictionary<int, Dictionary<char, char>>();

	private static string ShiftText(string text, int offset)
	{
		Dictionary<char, char> translation;
		if (translationCache.ContainsKey(offset)) {
			translation = translationCache[offset];
		} else {
			translation = new Dictionary<char, char>();
			AddShiftedElements(translation, VOWELS, offset);
			AddShiftedElements(translation, CONSONANTS_COMMON, offset);
			AddShiftedElements(translation, CONSONANTS_RARE, offset);
			translationCache[offset] = translation;
		}

		StringBuilder shifted = new StringBuilder(text.Length);
		for (int i = 0; i < text.Length; i++) {
			shifted.Append(translation[text[i]]);
		}
		return shifted.ToString();
	}

	private static void AddShiftedElements(Dictionary<char, char> dict, string elements, int offset)
	{
		for (int i = 0; i < elements.Length; i++) {
			int shifted = (i + offset) % elements.Length;
			if (shifted < 0)
				shifted += elements.Length;
			dict[elements[i]] = elements[shifted];
		}
	}
}
