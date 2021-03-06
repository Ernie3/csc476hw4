﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public enum GameMode
{
    preGame, // before game starts
    loading, // word list is loading and being parse
    makeLevel, // individual WordLevel is being created
    levelPrep, // level visuals are instantiated
    inLevel, // level is in progress
    gameOver
}

public class WordGame : MonoBehaviour
{
    public static WordGame S;

    [Header("Set in Inspector")]
    public GameObject prefabLetter;
    public Text levelText;
    public Text nextLevelText;
    public Text highScoreText;
    public Text timerText;
    public Text gameOverText;
    public Rect wordArea = new Rect(-24, 19, 48, 28);
    public float letterSize = 1.5f;
    public bool showAllWyrds = true;
    public float bigLetterSize = 4f;
    public Color bigColorDim = new Color(0.8f, 0.8f, 0.8f);
    public Color bigColorSelected = new Color(1f, 1f, 1f);
    public Vector3 bigLetterCenter = new Vector3(0, -16, 0);
    public Color[] wyrdPalette;

    [Header("Set Dynamically")]
    public GameMode mode = GameMode.preGame;
    public WordLevel currLevel;
    public int levelNumber;
    public float levelTime;
    public float restartTime;
    public List<Wyrd> wyrds;
    public List<Letter> bigLetters;
    public List<Letter> bigLettersActive;
    public string testWord;

    public static int Level => S.levelNumber;
    public static string HighScorePointsKey => "HighScorePoints";
    public static string HighScoreRoundsKey => "HighScoreRounds";

    private const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private Transform letterAnchor, bigLetterAnchor;
    private List<GameObject> goLetters = new List<GameObject>(), goBigLetters = new List<GameObject>();

    void Awake()
    {
        S = this;
        letterAnchor = new GameObject("LetterAnchor").transform;
        bigLetterAnchor = new GameObject("BigLetterAnchor").transform;
    }

    // Start is called before the first frame update
    void Start()
    {
        mode = GameMode.loading;
        WordList.INIT();
        HideEndGame();
    }

    // Update is called once per frame
    void Update()
    {
        Letter ltr;
        char c;
        switch(mode)
        {
            case GameMode.gameOver:
                gameOverText.text = "Game Over!" + System.Environment.NewLine;
                gameOverText.text += "Restarting in " + (int)restartTime;
                restartTime -= Time.deltaTime;
                break;

            case GameMode.inLevel:
                levelTime -= Time.deltaTime;

                if (levelTime <= 0)
                {
                    mode = GameMode.gameOver;
                    ClearLevel();
                    ShowEndGame();
                }

                var minutes = (int)levelTime / 60;
                var seconds = (int)levelTime % 60;

                timerText.text = minutes + ":" + (seconds == 0 ? "00" : seconds < 10 ? "0" + seconds : seconds.ToString());

                foreach (var cIt in Input.inputString)
                {
                    c = char.ToUpperInvariant(cIt);
                    // check to see if c is an upper case letter
                    if(upperCase.Contains(c))
                    {
                        // find available Letter in bigLetters with c
                        ltr = FindNextLetterByChar(c);
                        // if a Letter was returned
                        if(ltr != null)
                        {
                            // add c to the testWord and move
                            // the returned big letter to bigLettersActive
                            testWord += c.ToString();

                            // move it from the inactive to active list
                            bigLettersActive.Add(ltr);
                            bigLetters.Remove(ltr);
                            ltr.color = bigColorSelected;

                            ArrangeBigLetters();
                        }
                    }

                    if (c == '\b') // backspace
                    {
                        if (bigLettersActive.Count == 0)
                            return;

                        if (testWord.Length > 1)
                            testWord = testWord.Substring(0, testWord.Length - 1);
                        else
                            testWord = string.Empty;

                        ltr = bigLettersActive[bigLettersActive.Count - 1];
                        // move it from the active to the inactive list
                        bigLettersActive.Remove(ltr);
                        bigLetters.Add(ltr);
                        ltr.color = bigColorDim;

                        ArrangeBigLetters();
                    }

                    if (c == '\n' || c == '\r')
                    {
                        CheckWord();
                    }

                    if (c == ' ')
                    {
                        bigLetters = ShuffleLetters(bigLetters);
                        ArrangeBigLetters();
                    }
                }
                break;
        }
    }
    
    // finds an available Letter with the char c in bigLetters
    // if there isn't one available, it returns null
    Letter FindNextLetterByChar(char c)
    {
        foreach(var ltr in bigLetters)
        {
            if (ltr.c == c)
            {
                return ltr;
            }
        }
        return null;
    }

    public void CheckWord()
    {
        string subWord;
        var foundTestWord = false;
        var gotTargetWord = testWord == currLevel.word;

        // create a List<int> to hold the indicies of other subWords that are
        // contained within testWord
        var containedWords = new List<int>();

        for (var i = 0; i < currLevel.subWords.Count; i++)
        {
            if (wyrds[i].found)
                continue;

            subWord = currLevel.subWords[i];

            if(string.Equals(testWord, subWord))
            {
                HighlightWyrd(i);
                ScoreManager.SCORE(wyrds[i], 1);
                foundTestWord = true;
            }
            else if (testWord.Contains(subWord))
            {
                containedWords.Add(i);
            }
        }

        if (foundTestWord)
        {
            var numContained = containedWords.Count;
            int ndx;
            for (var i = 0; i < containedWords.Count; i++)
            {
                ndx = numContained - i - 1;
                HighlightWyrd(containedWords[ndx]);
                ScoreManager.SCORE(wyrds[containedWords[ndx]], i + 2);
            }
        }

        ClearBigLettersActive();

        if (gotTargetWord)
        {
            Invoke(nameof(NextLevel), 4f);
        }
    }

    void HighlightWyrd(int ndx)
    {
        // activate sub word, lighten color, make text visible
        wyrds[ndx].found = true;
        wyrds[ndx].color = (wyrds[ndx].color + Color.white) / 2f;
        wyrds[ndx].visible = true;
    }

    void ClearBigLettersActive()
    {
        testWord = string.Empty;
        foreach(var ltr in bigLettersActive)
        {
            bigLetters.Add(ltr);
            ltr.color = bigColorDim;
        }
        bigLettersActive.Clear();
        ArrangeBigLetters();
    }

    public void WordListParseComplete()
    {
        mode = GameMode.makeLevel;
        currLevel = MakeWordLevel(1);
        Invoke(nameof(HideHighScoreText), 3f);
    }

    public void NextLevel()
    {
        mode = GameMode.makeLevel;
        nextLevelText.gameObject.SetActive(true);
        ClearLevel();
        currLevel = MakeWordLevel(++levelNumber);
        Invoke(nameof(HideNextLevelText), 3f);
    }

    public WordLevel MakeWordLevel(int levelNum)
    {
        levelTime = levelNum == 1 ? 120f : 80f;

        levelNumber = levelNum;
        levelText.text = "Level " + levelNum;

        var level = new WordLevel
        {
            longWordIndex = Random.Range(0, WordList.LONG_WORD_COUNT),
            levelNum = levelNum
        };
        level.word = WordList.GET_LONG_WORD(level.longWordIndex);
        level.charDict = WordLevel.MakeCharDict(level.word);

        StartCoroutine(FindSubWordsCoroutine(level));

        return level;
    }

    private WordLevel MakeWordLevel()
    {
        return MakeWordLevel(1);
    }

    public IEnumerator FindSubWordsCoroutine(WordLevel level)
    {
        level.subWords = new List<string>();
        string str;

        var words = WordList.GET_WORDS();

        for (var i = 0; i < WordList.WORD_COUNT; i++)
        {
            str = words[i];

            if (WordLevel.CheckWordInLevel(str, level))
                level.subWords.Add(str);

            if (i % WordList.NUM_TO_PARSE_BEFORE_YIELD == 0)
                yield return null;
        }

        level.subWords.Sort();
        level.subWords = SortWordsByLength(level.subWords).ToList();

        SubWordSearchComplete();
    }

    public static IEnumerable<string> SortWordsByLength(IEnumerable<string> ws)
    {
        return ws.OrderBy(s => s.Length);
    }

    public void SubWordSearchComplete()
    {
        mode = GameMode.levelPrep;
        Layout();
    }

    void Layout()
    {
        wyrds = new List<Wyrd>();

        GameObject go;
        Letter lett;
        string word;
        Vector3 pos;
        float left = 0;
        float columnWidth = 3;
        char c;
        Color col;
        Wyrd wyrd;

        var numRows = Mathf.RoundToInt(wordArea.height / letterSize);

        for (var i = 0; i < currLevel.subWords.Count; i++)
        {
            wyrd = new Wyrd();
            word = currLevel.subWords[i];

            // if the word is longer than columnWidth, expand it
            columnWidth = Mathf.Max(columnWidth, word.Length);

            for (var j = 0; j < word.Length; j++)
            {
                c = word[j];
                go = Instantiate(prefabLetter);
                go.transform.SetParent(letterAnchor);
                lett = go.GetComponent<Letter>();
                lett.c = c;

                pos = new Vector3(wordArea.x + left + j * letterSize, wordArea.y, 0);

                pos.y -= (i % numRows) * letterSize; // modulus here makes multiple columns line up

                lett.posImmediate = pos + Vector3.up * (20 + i % numRows);

                lett.pos = pos;

                // increment lett.timeStart to move wyrds at different times
                lett.timeStart = Time.time + i * 0.5f;

                go.transform.localScale = Vector3.one * letterSize;

                wyrd.Add(lett);
                goLetters.Add(go);
            }

            if (showAllWyrds)
                wyrd.visible = true;

            wyrd.color = wyrdPalette[word.Length - WordList.WORD_LENGTH_MIN];

            wyrds.Add(wyrd);

            // if we've gotten to the numRows(th) row, start a new column
            if (i % numRows == numRows - 1)
                left += (columnWidth + 0.5f) * letterSize;
        }

        bigLetters = new List<Letter>();
        bigLettersActive = new List<Letter>();

        for (var i = 0; i < currLevel.word.Length; i++)
        {
            c = currLevel.word[i];
            go = Instantiate(prefabLetter);
            go.transform.SetParent(bigLetterAnchor);
            lett = go.GetComponent<Letter>();
            lett.c = c;
            go.transform.localScale = Vector3.one * bigLetterSize;
            // set the initial position of the big letters below the screen
            pos = new Vector3(0, -100, 0);
            lett.posImmediate = pos;
            lett.pos = pos;

            lett.timeStart = Time.time + currLevel.subWords.Count * 0.05f;
            lett.easingCurve = Easing.Sin + "-0.18"; // bouncy easing

            col = bigColorDim;
            lett.color = col;
            lett.visible = true; // always true for big letters
            lett.big = true;
            bigLetters.Add(lett);
            goBigLetters.Add(go);
        }

        bigLetters = ShuffleLetters(bigLetters);
        ArrangeBigLetters();

        mode = GameMode.inLevel;
    }

    List<Letter> ShuffleLetters(List<Letter> letts)
    {
        var newL = new List<Letter>();
        int ndx;
        while(letts.Count > 0)
        {
            ndx = Random.Range(0, letts.Count);
            newL.Add(letts[ndx]);
            letts.RemoveAt(ndx);
        }
        return newL;
    }

    void ArrangeBigLetters()
    {
        var halfWidth = ((float)bigLetters.Count) / 2f - 0.5f;
        Vector3 pos;
        for (var i = 0; i < bigLetters.Count; i++)
        {
            pos = bigLetterCenter;
            pos.x += (i - halfWidth) * bigLetterSize;
            bigLetters[i].pos = pos;
        }
        // bigLettersActive
        halfWidth = ((float)bigLettersActive.Count) / 2f - 0.5f;
        for (var i = 0; i < bigLettersActive.Count; i++)
        {
            pos = bigLetterCenter;
            pos.x += (i - halfWidth) * bigLetterSize;
            pos.y += bigLetterSize * 1.25f;
            bigLettersActive[i].pos = pos;
        }
    }

    public static void ShowHighScore()
    {
        if (PlayerPrefs.HasKey(WordGame.HighScorePointsKey) && PlayerPrefs.HasKey(WordGame.HighScoreRoundsKey))
        {
            WordGame.S.highScoreText.text = "Current High Score: " + System.Environment.NewLine;
            WordGame.S.highScoreText.text += PlayerPrefs.GetInt(WordGame.HighScorePointsKey) + " points in " + PlayerPrefs.GetInt(WordGame.HighScoreRoundsKey) + " rounds";
            WordGame.S.highScoreText.gameObject.SetActive(true);
        }
        else
        {
            WordGame.S.highScoreText.text = "No high score set yet!";
            WordGame.S.highScoreText.gameObject.SetActive(true);
        }
    }

    private void ShowHighScoreInternal()
    {
        if (PlayerPrefs.HasKey(WordGame.HighScorePointsKey) && PlayerPrefs.HasKey(WordGame.HighScoreRoundsKey))
        {
            highScoreText.text = "Current High Score: " + System.Environment.NewLine;
            highScoreText.text += PlayerPrefs.GetInt(WordGame.HighScorePointsKey) + " points in " + PlayerPrefs.GetInt(WordGame.HighScoreRoundsKey) + " rounds";
            highScoreText.gameObject.SetActive(true);
        }
        else
        {
            highScoreText.text = "No high score set yet!";
            highScoreText.gameObject.SetActive(true);
        }
    }

    private void HideHighScore()
    {
        highScoreText.gameObject.SetActive(false);
    }

    private void ClearLevel()
    {
        goLetters.ForEach(go => Destroy(go));
        goBigLetters.ForEach(go => Destroy(go));
        goLetters.Clear();
        goBigLetters.Clear();
    }

    private void HideNextLevelText()
    {
        nextLevelText.gameObject.SetActive(false);
    }

    private void HideHighScoreText()
    {
        highScoreText.gameObject.SetActive(false);
    }

    private void ShowEndGame()
    {
        gameOverText.gameObject.SetActive(true);

        if (PlayerPrefs.GetInt(HighScorePointsKey, int.MinValue) < Scoreboard.S.score)
        {
            PlayerPrefs.SetInt(HighScorePointsKey, Scoreboard.S.score);
            PlayerPrefs.SetInt(HighScoreRoundsKey, levelNumber);
        }

        restartTime = 7f;
        levelNumber = 0;
        Invoke(nameof(HideEndGame), 7.5f);
        Invoke(nameof(ShowHighScoreInternal), 7.5f);
        Invoke(nameof(HideHighScore), 11.25f);
        Invoke(nameof(MakeWordLevel), 11.5f);
        Invoke(nameof(ResetScore), 11.5f);
    }

    private void HideEndGame()
    {
        gameOverText.gameObject.SetActive(false);
    }

    private void ResetScore()
    {
        Scoreboard.S.score = 0;
    }
}
