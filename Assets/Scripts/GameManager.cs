using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/* 
	blockColorList {
		"empty" : "black", // 0
		"I" : "cyan",	// 00FFFF
		"J" : "blue",	// 0000FF
		"L" : "orange",	// FFA500
		"O" : "yellow",	// FFFF00
		"S" : "lime",	// 80FF00
		"T" : "purple",	// 800080
		"Z" : "red",	// FF0000
		"outline" : "grey" // 808080
	}
*/

public class GameManager : Singleton<GameManager> {
	// singleton pattern
	protected GameManager () {} // guarantee this will be always a singleton only - can't use the constructor!

	private AudioSource clickAudio, scoreAudio, loseAudio;

	// board pieces
	public int sizeW = 12, sizeH = 24;
	public GameObject gridContainer, gridModel, gameOverPanel, menuPanel;

	// blocks
	private string[] blockShapesText = {
		"    "+
		"1111"+		// I
		"    "+
		"    ",

		" 1  "+
		" 1  "+		// I
		" 1  "+
		" 1  ",

		"    "+
		"1111"+		// I
		"    "+
		"    ",

		" 1  "+
		" 1  "+		// I
		" 1  "+
		" 1  ",

		"    "+
		"222 "+ 	// J
		"  2 "+
		"    ",

		" 2  "+
		" 2  "+ 	// J
		"22  "+
		"    ",

		"2   "+
		"222 "+ 	// J
		"    "+
		"    ",

		" 22 "+
		" 2  "+ 	// J
		" 2  "+
		"    ",

		"  3 "+
		"333 "+ 	// L
		"    "+
		"    ",

		" 3  "+
		" 3  "+ 	// L
		" 33 "+
		"    ",

		"    "+
		"333 "+ 	// L
		"3   "+
		"    ",

		"33  "+
		" 3  "+ 	// L
		" 3  "+
		"    ",

		"44  "+
		"44  "+ 	// O
		"    "+
		"    ",

		"44  "+
		"44  "+ 	// O
		"    "+
		"    ",

		"44  "+
		"44  "+ 	// O
		"    "+
		"    ",

		"44  "+
		"44  "+ 	// O
		"    "+
		"    ",

		" 55 "+
		"55  "+ 	// S
		"    "+
		"    ",

		" 5  "+
		" 55 "+ 	// S
		"  5 "+
		"    ",

		" 55 "+
		"55  "+ 	// S
		"    "+
		"    ",

		" 5  "+
		" 55 "+ 	// S
		"  5 "+
		"    ",

		"    "+
		"666 "+ 	// T
		" 6  "+
		"    ",

		" 6  "+
		"66  "+ 	// T
		" 6  "+
		"    ",

		" 6  "+
		"666 "+ 	// T
		"    "+
		"    ",

		" 6  "+
		" 66 "+ 	// T
		" 6  "+
		"    ",

		"77  "+
		" 77 "+ 	// Z
		"    "+
		"    ",

		"  7 "+
		" 77 "+ 	// Z
		" 7  "+
		"    ",

		"77  "+
		" 77 "+ 	// Z
		"    "+
		"    ",

		"  7 "+
		" 77 "+ 	// Z
		" 7  "+
		"    ",
	};
	private string[] blockColorLists = {
		"303030", // dark grey
		"00FFFF", // cyan
		"0000FF", // blue 
		"FFA500", // orange
		"FFFF00", // yellow
		"80FF00", // lime
		"FF00FF", // purple
		"FF0000", // red
		"000000"  // black
	};
	int[,] blockShapes = null;
	private List<Color> blockColorList = null;

	// grid arrays
	private int numCells = 0;
	private List<int> boardGrid = null;
	private List<GameObject> spriteGrid = null;
	public Vector2 gridStep = Vector2.one, gridOrigin = Vector2.zero;

	// gameplay
	public Text scoreText, levelText, finalScoreText, finalLevelText;
	public int levelLineMax = 10; 			// number of lines to clear before leveling up
	public float levelSpeedFactor = .9f;	// 90% of previous drop delay 
	private static int score = 0, prevLineCount = 0, level = 1, levelLineCount = 0;
	private bool isPaused = false, isBlockFalling = false, isDestroyLine = false, isGameOver = false;
	private int blockPosX, blockPosY; // grid location offset for falling block (relative to 0,0 in blockPattern)
	private int[] blockPattern = new int[16]; // 4 x 4 pattern grid, updated with current block type and rotation, (0,0) is lower left
	private int blockIndex = 0; // index into blockShapeList array (including all rotations)
	public float fallDelayMax = .5f, buttonDelay = .35f, blinkSpeed = .05f, blinkDelay = .25f;
	private float nextFall = 0f, nextButton = 0f, nextBlink = 0f, endBlink = 0f, fallDelay = .5f;

	void Start()
	{
		AudioSource[] audio = GetComponents<AudioSource>();
		clickAudio = audio[0];
		scoreAudio = audio[1];
		loseAudio = audio[2];

		// create data structures (if any)
		bool success = InitBoard();
		if( success )
		{
			NewGame();
		}
	}

	public void ExitGame()
	{
		Application.Quit();
	}
	public void ResumeGame(bool toggle = true)
	{
		isPaused = toggle;
		menuPanel.SetActive(isPaused);
		gameOverPanel.SetActive(isGameOver && !isPaused);
	}
	public void MenuButton()
	{
		Debug.Log("MenuButton");
		ResumeGame(!isPaused);
	}
	void Update()
	{
		if( scoreText != null )
			scoreText.text = "Score: " + score;

		if( levelText != null )
			levelText.text = "Level: " + level;

		if( Input.GetKeyDown(KeyCode.Escape) )
		{
			Debug.Log("Escape");
			ResumeGame(!isPaused);
		}

		if( !isGameOver && !isPaused && !isBlockFalling && isDestroyLine )
		{
			if( Time.time > endBlink )
			{
				CollapseLines();
				isDestroyLine = false;
			}
			else
			{
				// change the color of sprites on the grid
				BlinkLines();
			}
		}

		if( !isGameOver && !isPaused && !isBlockFalling && !isDestroyLine )
		{
			SpawnBlock();
		}
		
		if( !isGameOver && !isPaused && isBlockFalling && !isDestroyLine )
		{
			// player wants to move block
			if( Input.GetKeyDown(KeyCode.LeftArrow) )
			{
				nextButton = Time.time + buttonDelay;
				if( TryMove(blockPosX-1, blockPosY, blockPattern) )
				{
					blockPosX--;
					clickAudio.Play();
				}
			}
			if( Input.GetKeyDown(KeyCode.RightArrow) )
			{
				nextButton = Time.time + buttonDelay;
				if( TryMove(blockPosX+1, blockPosY, blockPattern) )
				{
					blockPosX++;
					clickAudio.Play();
				}
			}

			// player wants to rotate block
			if( Input.GetKeyDown(KeyCode.UpArrow) )
			{
				nextButton = Time.time + buttonDelay;
				if(TryRotate())
				{
					DoRotate();
					clickAudio.Play();
				}
			}

			// down key
			if( Input.GetKeyDown(KeyCode.DownArrow) )
			{
				clickAudio.Play();
			}

			// regular gravity or player wants to speed up block
			if( Time.time > nextFall || (Input.GetKey(KeyCode.DownArrow) && Time.time > nextFall - fallDelay*.5) || Input.GetKeyDown(KeyCode.DownArrow))
			{
				nextFall = Time.time + fallDelay;
				nextButton = Time.time + buttonDelay;
				if( TryMove(blockPosX, blockPosY-1, blockPattern) )
					blockPosY--;
				else
					processDownHit();
			}
			// change the color of sprites on the grid
			refreshGridDisplay();
		}
	}

	// change the color of sprites on the grid
	void refreshGridDisplay()
	{
		// draw permanent cells
		for( int y = 0, s = 0; y < sizeH; y++)
			for( int x = 0; x < sizeW; x++, s++)
				if( s < boardGrid.Count) 
					if( boardGrid[s] >= 0 )
						SetGridColor( x, y, boardGrid[s] );
//					else	
//						SetGridColor( x, y, Random.Range(1, blockColorList.Count));

		// over-draw player block
		if( isBlockFalling )
			for( int y = 0, s = 0; y < 4; y++)
				for( int x = 0; x < 4; x++, s++)
					if( s < blockPattern.Length && blockPattern[s] != 0 )
						SetGridColor( blockPosX+x, blockPosY+y, blockPattern[s] ); 
	}

	// block has run into something and stopped moving
	void processDownHit()  
	{
		// turn off player controls and block drawing
		isBlockFalling = false;

		// transfer block to permanent grid
		for( int y = 0, s = 0; y < 4; y++)
		for( int x = 0; x < 4; x++, s++)
		{
			// valid cell of player block
			if( blockPosY+y >= 0 && s < blockPattern.Length && blockPattern[s] != 0 )
			{
				// block is above top line
				if( blockPosY+y >= sizeH )
					isGameOver = true;
				else {
					int coord = blockPosX+x + sizeW * (blockPosY+y);
					if( coord < boardGrid.Count )
						boardGrid[coord] = blockPattern[s];
				}
			}
		}

		if( isGameOver )
		{
			if( finalScoreText != null )
				finalScoreText.text = "Score: " + score;
			if( finalLevelText != null )
				finalLevelText.text = "Level: " + level;

			gameOverPanel.SetActive(true);

			loseAudio.Play();
			Debug.Log("game over");
			return;
		}

		// check for line completions
		for( int y = 0; y < 4; y++)
		{
			bool lineFlag = true;
			for( int x = 0; lineFlag && x < sizeW; x++)
			{
				int coord = x + sizeW * (blockPosY+y);
				if( blockPosY+y < 0 || coord >= boardGrid.Count || boardGrid[coord] == 0 )
					lineFlag = false;
			}
			if( lineFlag )
			{
				isDestroyLine = true;
				endBlink = Time.time + blinkDelay;
				// mark cells for destruction
				for( int x = 0; lineFlag && x < sizeW; x++)
				{
					int coord = x + sizeW * (blockPosY+y);
					if( coord < boardGrid.Count)
						boardGrid[coord] *= -1;
				}
			}
		}
	}

	bool TryRotate()
	{
		int[] pattern = new int[16]; // 4 x 4 pattern grid, (0,0) is lower left

		// find next rotation
		int index = blockIndex - (blockIndex % 4) + (blockIndex + 1) % 4;

		// copy into temporary pattern array
		System.Array.Clear( pattern, 0, pattern.Length);
		for( int i = 0; i < 16 && i < pattern.Length; i++)
			pattern[i] = blockShapes[index,i];

		return TryMove( blockPosX, blockPosY, pattern);
	}

	void DoRotate()
	{
		// find next rotation
		blockIndex = blockIndex - (blockIndex % 4) + (blockIndex + 1) % 4;

		// copy shape into active pattern array
		System.Array.Clear( blockPattern, 0, blockPattern.Length);
		for( int i = 0; i < 16 && i < blockPattern.Length; i++)
			blockPattern[i] = blockShapes[blockIndex,i];
	}

	bool TryMove( int testX, int testY, int[] pattern)
	{
		//bool hitGrid = false, hitSide = false, hitBottom = false;
		
		// check for collisions
		for( int y = 0, s = 0; y < 4; y++)
			for( int x = 0; x < 4; x++, s++)
			{
				if( pattern[s] != 0 )
				{
					if( testX + x < 0 || testX + x >= sizeW )
						return false;

					if( testY + y < 0 )
						return false;

					int coord = testX+x + sizeW*(testY+y);
					if( coord < boardGrid.Count && boardGrid[coord] != 0 )
						return false;
				}
			}

		// !hitGrid && !hitSide && !hitBottom
		return true;
	}

	void SpawnBlock( )
	{
		// get random block
		blockIndex = Random.Range(0, blockShapes.GetLength(0));

		// copy shape into active pattern array
		System.Array.Clear( blockPattern, 0, blockPattern.Length);
		for( int i = 0; i < 16 && i < blockPattern.Length; i++)
			blockPattern[i] = blockShapes[blockIndex,i];

		// set spawn location (top centre)
		blockPosX = sizeW / 2 - 1;
		blockPosY = sizeH;

		// proceed with gravity
		isBlockFalling = true;
	}

	public void AddScore(int lines)
	{
		if(lines > 0)
			scoreAudio.Play();

		// score 100 for each line
		score += lines * 100;

		// score 400 bonus for 4-line-multi
		if( lines == 4 )
		{
			score += 400;

			// score points for consecutive 4-line-multi
			if( prevLineCount == 4 )
				score += 400;
		}

		// store line count for next line collapse
		prevLineCount = lines;

		// keep track of total line count
		levelLineCount += lines;

		if( levelLineCount > levelLineMax )
		{
			// level up
			level++;
			levelLineCount -= levelLineMax;

			// speed up falling blocks
			fallDelay *= levelSpeedFactor;
		}
	}

	// setup board for new game
	public void NewGame()
	{
		Debug.Log("new game");

		gameOverPanel.SetActive(false);
		menuPanel.SetActive(false);

		score = 0;
		level = 1;
		levelLineCount = 0;
		prevLineCount = 0;
		fallDelay = fallDelayMax;

		for( int i = 0; i < numCells; i++ )
		{
			// wipe board grid
			boardGrid[i] = 0;

			// hide blocks
			int x = i % sizeW;
			int y = i / sizeW;
			SetGridColor( x, y, 0 );
		}

		// reset game state
		isPaused = false;
		isDestroyLine = false;
		isGameOver = false;

		// spawn first block
		SpawnBlock();
	}

	// initialize board
	bool InitBoard()
	{
		// don't init structures twice
		if( boardGrid != null)
			return false;

		// decode block colors
		blockColorList = new List<Color>();
		foreach( string s in blockColorLists )
		{
			Color newCol = new Color( 	// "RRGGBB" hex color
				(float)System.Convert.ToInt32( s.Substring(0, 2), 16 ) / 255f, // red 0..1 
				(float)System.Convert.ToInt32( s.Substring(2, 2), 16 ) / 255f, // grn 0..1
				(float)System.Convert.ToInt32( s.Substring(4, 2), 16 ) / 255f  // blu 0..1
			);
			blockColorList.Add(newCol);
		}

		// parse blockShapes
		blockShapes = new int[blockShapesText.Length, 16];
		for(int i = 0; i < blockShapesText.Length; i++ )
			for(int j = 0; j < blockShapesText[i].Length; j++ )
				blockShapes[i,j] = (blockShapesText[i][j] == ' ') ? 0 : blockShapesText[i][j] - '0';  

		// init board grid to zero
		numCells = sizeW * sizeH;
		boardGrid = new List<int>(numCells);
		spriteGrid = new List<GameObject>(numCells);
	
		for(int i = 0; i < numCells; i++ )
		{
			// wipe board grid state
			boardGrid.Add(0);

			// create grid cell object
			if( gridModel != null && gridContainer != null )
			{
				int x = i % sizeW;
				int y = i / sizeW;

				Vector2 target = new Vector2( x * gridStep.x, y * gridStep.y ) + gridOrigin;
				GameObject cell = (GameObject) Instantiate(gridModel, target, Quaternion.identity);
				cell.transform.SetParent(gridContainer.transform);

				// save sprite object for later
				spriteGrid.Add(cell);

				// set color (depends on spriteGrid)
				SetGridColor(x, y, 0);
			}
		}

		return true;
	}

	void SetGridColor(int x, int y, int j)
	{
		if( x < 0 || y < 0 || x >= sizeW || y >= sizeH || j >= blockColorList.Count)
			return;

		GameObject cell = spriteGrid[x + y * sizeW];
		SpriteRenderer sr = cell.GetComponent<SpriteRenderer>();
		sr.color = blockColorList[j];
	}

	void BlinkLines()
	{
		if( Time.time > nextBlink )
		{
			nextBlink = Time.time + blinkSpeed;
			for(int y = 0, s = 0; y < sizeH; y++ )
			{
				int j = Random.Range(1, blockColorList.Count);
				for(int x = 0; x < sizeW; x++, s++ )
					if( boardGrid[s] < 0 )
					{
						GameObject cell = spriteGrid[s];
						SpriteRenderer sr = cell.GetComponent<SpriteRenderer>();
						sr.color = blockColorList[j];
					}
			}
		}
	}
	void CollapseLines()
	{
		int lineCount = 0;
		for(int y = 0; y < sizeH; y++ )
		{
			if( boardGrid[sizeW * y] < 0 )
			{
				lineCount++;

				// copy down lines from above
				int s1 = sizeW * y, s2 = s1 + sizeW;
				while( s2 < boardGrid.Count )
					boardGrid[s1++] = boardGrid[s2++];

				while( s1 < boardGrid.Count )
					boardGrid[s1++] = 0;
				
				// check this line again
				y--;
			}
		}

		AddScore( lineCount );
	}

}

