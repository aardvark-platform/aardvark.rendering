namespace Aardvark.Application

open Aardvark.Base
open System.Runtime.CompilerServices
open FSharp.Data.Adaptive

type Keys =
    /// No key pressed.
    | None = 0

    /// The CANCEL key.
    | Cancel = 1

    /// The BACKSPACE key.
    | Back = 2

    /// The TAB key.
    | Tab = 3

    /// The LineFeed key.
    | LineFeed = 4

    /// The CLEAR key.
    | Clear = 5

    /// The RETURN key.
    | Return = 6

    /// The ENTER key.
    | Enter = 6 // = Return

    /// The PAUSE key.
    | Pause = 7

    /// The CAPS LOCK key.
    | Capital = 8

    /// The CAPS LOCK key.
    | CapsLock = 8

    /// The IME Kana mode key.
    | KanaMode = 9

    /// The IME Hangul mode key.
    | HangulMode = 9

    /// The IME Junja mode key.
    | JunjaMode = 10

    /// The IME Final mode key.
    | FinalMode = 11

    /// The IME Hanja mode key.
    | HanjaMode = 12

    /// The IME Kanji mode key.
    | KanjiMode = 12

    /// The ESC key.
    | Escape = 13

    /// The IME Convert key.
    | ImeConvert = 14

    /// The IME NonConvert key.
    | ImeNonConvert = 15

    /// The IME Accept key.
    | ImeAccept = 16

    /// The IME Mode change request.
    | ImeModeChange = 17

    /// The SPACEBAR key.
    | Space = 18

    /// The PAGE UP key.
    | Prior = 19

    /// The PAGE UP key.
    | PageUp = 19

    /// The PAGE DOWN key.
    | Next = 20

    /// The PAGE DOWN key.
    | PageDown = 20

    /// The END key.
    | End = 21

    /// The HOME key.
    | Home = 22

    /// The LEFT ARROW key.
    | Left = 23

    /// The UP ARROW key.
    | Up = 24

    /// The RIGHT ARROW key.
    | Right = 25

    /// The DOWN ARROW key.
    | Down = 26

    /// The SELECT key.
    | Select = 27

    /// The PRINT key.
    | Print = 28

    /// The EXECUTE key.
    | Execute = 29

    /// The PRINT SCREEN key.
    | Snapshot = 30

    /// The PRINT SCREEN key.
    | PrintScreen = 30

    /// The INS key.
    | Insert = 31

    /// The DEL key.
    | Delete = 32

    /// The HELP key.
    | Help = 33

    /// The 0 key.
    | D0 = 34

    /// The 1 key.
    | D1 = 35

    /// The 2 key.
    | D2 = 36

    /// The 3 key.
    | D3 = 37

    /// The 4 key.
    | D4 = 38

    /// The 5 key.
    | D5 = 39

    /// The 6 key.
    | D6 = 40

    /// The 7 key.
    | D7 = 41

    /// The 8 key.
    | D8 = 42

    /// The 9 key.
    | D9 = 43

    /// The A key.
    | A = 44

    /// The B key.
    | B = 45

    /// The C key.
    | C = 46

    /// The D key.
    | D = 47

    /// The E key.
    | E = 48

    /// The F key.
    | F = 49

    /// The G key.
    | G = 50

    /// The H key.
    | H = 51

    /// The I key.
    | I = 52

    /// The J key.
    | J = 53

    /// The K key.
    | K = 54

    /// The L key.
    | L = 55

    /// The M key.
    | M = 56

    /// The N key.
    | N = 57

    /// The O key.
    | O = 58

    /// The P key.
    | P = 59

    /// The Q key.
    | Q = 60

    /// The R key.
    | R = 61

    /// The S key.
    | S = 62

    /// The T key.
    | T = 63

    /// The U key.
    | U = 64

    /// The V key.
    | V = 65

    /// The W key.
    | W = 66

    /// The X key.
    | X = 67

    /// The Y key.
    | Y = 68

    /// The Z key.
    | Z = 69

    /// The left Windows logo key (Microsoft Natural Keyboard).
    | LWin = 70

    /// The right Windows logo key (Microsoft Natural Keyboard).
    | RWin = 71

    /// The Application key (Microsoft Natural Keyboard).
    | Apps = 72

    /// The Computer Sleep key.
    | Sleep = 73

    /// The 0 key on the numeric keypad.
    | NumPad0 = 74

    /// The 1 key on the numeric keypad.
    | NumPad1 = 75

    /// The 2 key on the numeric keypad.
    | NumPad2 = 76

    /// The 3 key on the numeric keypad.
    | NumPad3 = 77

    /// The 4 key on the numeric keypad.
    | NumPad4 = 78

    /// The 5 key on the numeric keypad.
    | NumPad5 = 79

    /// The 6 key on the numeric keypad.
    | NumPad6 = 80

    /// The 7 key on the numeric keypad.
    | NumPad7 = 81

    /// The 8 key on the numeric keypad.
    | NumPad8 = 82

    /// The 9 key on the numeric keypad.
    | NumPad9 = 83

    /// The Multiply key.
    | Multiply = 84

    /// The Add key.
    | Add = 85

    /// The Separator key.
    | Separator = 86

    /// The Subtract key.
    | Subtract = 87

    /// The Decimal key.
    | Decimal = 88

    /// The Divide key.
    | Divide = 89

    /// The F1 key.
    | F1 = 90

    /// The F2 key.
    | F2 = 91

    /// The F3 key.
    | F3 = 92

    /// The F4 key.
    | F4 = 93

    /// The F5 key.
    | F5 = 94

    /// The F6 key.
    | F6 = 95

    /// The F7 key.
    | F7 = 96

    /// The F8 key.
    | F8 = 97

    /// The F9 key.
    | F9 = 98

    /// The F10 key.
    | F10 = 99

    /// The F11 key.
    | F11 = 100

    /// The F12 key.
    | F12 = 101

    /// The F13 key.
    | F13 = 102

    /// The F14 key.
    | F14 = 103

    /// The F15 key.
    | F15 = 104

    /// The F16 key.
    | F16 = 105

    /// The F17 key.
    | F17 = 106

    /// The F18 key.
    | F18 = 107

    /// The F19 key.
    | F19 = 108

    /// The F20 key.
    | F20 = 109

    /// The F21 key.
    | F21 = 110

    /// The F22 key.
    | F22 = 111

    /// The F23 key.
    | F23 = 112

    /// The F24 key.
    | F24 = 113

    /// The NUM LOCK key.
    | NumLock = 114

    /// The SCROLL LOCK key.
    | Scroll = 115

    /// The left SHIFT key.
    | LeftShift = 116

    /// The right SHIFT key.
    | RightShift = 117

    /// The left CTRL key.
    | LeftCtrl = 118

    /// The right CTRL key.
    | RightCtrl = 119

    /// The left ALT key.
    | LeftAlt = 120

    /// The right ALT key.
    | RightAlt = 121

    /// The Browser Back key.
    | BrowserBack = 122

    /// The Browser Forward key.
    | BrowserForward = 123

    /// The Browser Refresh key.
    | BrowserRefresh = 124

    /// The Browser Stop key.
    | BrowserStop = 125

    /// The Browser Search key.
    | BrowserSearch = 126

    /// The Browser Favorites key.
    | BrowserFavorites = 127

    /// The Browser Home key.
    | BrowserHome = 128

    /// The Volume Mute key.
    | VolumeMute = 129

    /// The Volume Down key.
    | VolumeDown = 130

    /// The Volume Up key.
    | VolumeUp = 131

    /// The Media Next Track key.
    | MediaNextTrack = 132

    /// The Media Previous Track key.
    | MediaPreviousTrack = 133

    /// The Media Stop key.
    | MediaStop = 134

    /// The Media Play Pause key.
    | MediaPlayPause = 135

    /// The Launch Mail key.
    | LaunchMail = 136

    /// The Select Media key.
    | SelectMedia = 137

    /// The Launch Application1 key.
    | LaunchApplication1 = 138

    /// The Launch Application2 key.
    | LaunchApplication2 = 139

    /// The Oem 1 key.
    | Oem1 = 140

    /// The Oem Semicolon key.
    | OemSemicolon = 140

    /// The Oem plus key.
    | OemPlus = 141

    /// The Oem comma key.
    | OemComma = 142

    /// The Oem Minus key.
    | OemMinus = 143

    /// The Oem Period key.
    | OemPeriod = 144

    /// The Oem 2 key.
    | Oem2 = 145

    /// The Oem Question key.
    | OemQuestion = 145

    /// The Oem 3 key.
    | Oem3 = 146

    /// The Oem tilde key.
    | OemTilde = 146

    /// The ABNT_C1 (Brazilian) key.
    | AbntC1 = 147

    /// The ABNT_C2 (Brazilian) key.
    | AbntC2 = 148

    /// The Oem 4 key.
    | Oem4 = 149

    /// The Oem Open Brackets key.
    | OemOpenBrackets = 149

    /// The Oem 5 key.
    | Oem5 = 150

    /// The Oem Pipe key.
    | OemPipe = 150

    /// The Oem 6 key.
    | Oem6 = 151

    /// The Oem Close Brackets key.
    | OemCloseBrackets = 151

    /// The Oem 7 key.
    | Oem7 = 152

    /// The Oem Quotes key.
    | OemQuotes = 152

    /// The Oem8 key.
    | Oem8 = 153

    /// The Oem 102 key.
    | Oem102 = 154

    /// The Oem Backslash key.
    | OemBackslash = 154


    /// A special key masking the real key being processed by an IME.
    | ImeProcessed = 155

    /// A special key masking the real key being processed as a system key.
    | System = 156


    /// The OEM_ATTN key.
    | OemAttn = 157

    /// The DBE_ALPHANUMERIC key.
    | DbeAlphanumeric = 157

    /// The OEM_FINISH key.
    | OemFinish = 158

    /// The DBE_KATAKANA key.
    | DbeKatakana = 158

    /// The OEM_COPY key.
    | OemCopy = 159

    /// The DBE_HIRAGANA key.
    | DbeHiragana = 159

    /// The OEM_AUTO key.
    | OemAuto = 160

    /// The DBE_SBCSCHAR key.
    | DbeSbcsChar = 160

    /// The OEM_ENLW key.
    | OemEnlw = 161

    /// The DBE_DBCSCHAR key.
    | DbeDbcsChar = 161

    /// The OEM_BACKTAB key.
    | OemBackTab = 162

    /// The DBE_ROMAN key.
    | DbeRoman = 162

    /// The ATTN key.
    | Attn = 163

    /// The DBE_NOROMAN key.
    | DbeNoRoman = 163

    /// The CRSEL key.
    | CrSel = 164

    /// The DBE_ENTERWORDREGISTERMODE key.
    | DbeEnterWordRegisterMode = 164

    /// The EXSEL key.
    | ExSel = 165

    /// The DBE_ENTERIMECONFIGMODE key.
    | DbeEnterImeConfigureMode = 165

    /// The ERASE EOF key.
    | EraseEof = 166

    /// The DBE_FLUSHSTRING key.
    | DbeFlushString = 166

    /// The PLAY key.
    | Play = 167

    /// The DBE_CODEINPUT key.
    | DbeCodeInput = 167

    /// The ZOOM key.
    | Zoom = 168

    /// The DBE_NOCODEINPUT key.
    | DbeNoCodeInput = 168

    /// A constant reserved for future use.
    | NoName = 169

    /// The DBE_DETERMINESTRING key.
    | DbeDetermineString = 169

    /// The PA1 key.
    | Pa1 = 170

    /// The DBE_ENTERDLGCONVERSIONMODE key.
    | DbeEnterDialogConversionMode = 170

    /// The CLEAR key.
    | OemClear = 171

    /// Indicates the key is part of a dead-key composition
    | DeadCharProcessed = 172

module VirtualKeys =
    
    [<Literal>]
    let QS_EVENT = 0x2000
    [<Literal>]
    let VK_CANCEL = 0x03
    [<Literal>]
    let VK_BACK = 0x08
    [<Literal>]
    let VK_CLEAR = 0x0C
    [<Literal>]
    let VK_RETURN = 0x0D
    [<Literal>]
    let VK_PAUSE = 0x13
    [<Literal>]
    let VK_CAPITAL = 0x14
    [<Literal>]
    let VK_KANA = 0x15
    [<Literal>]
    let VK_HANGEUL = 0x15
    [<Literal>]
    let VK_HANGUL = 0x15
    [<Literal>]
    let VK_JUNJA = 0x17
    [<Literal>]
    let VK_FINAL = 0x18
    [<Literal>]
    let VK_HANJA = 0x19
    [<Literal>]
    let VK_KANJI = 0x19
    [<Literal>]
    let VK_ESCAPE = 0x1B
    [<Literal>]
    let VK_CONVERT = 0x1C
    [<Literal>]
    let VK_NONCONVERT = 0x1D
    [<Literal>]
    let VK_ACCEPT = 0x1E
    [<Literal>]
    let VK_MODECHANGE = 0x1F
    [<Literal>]
    let VK_SPACE = 0x20
    [<Literal>]
    let VK_PRIOR = 0x21
    [<Literal>]
    let VK_NEXT = 0x22
    [<Literal>]
    let VK_END = 0x23
    [<Literal>]
    let VK_HOME = 0x24
    [<Literal>]
    let VK_LEFT = 0x25
    [<Literal>]
    let VK_UP = 0x26
    [<Literal>]
    let VK_RIGHT = 0x27
    [<Literal>]
    let VK_DOWN = 0x28
    [<Literal>]
    let VK_SELECT = 0x29
    [<Literal>]
    let VK_PRINT = 0x2A
    [<Literal>]
    let VK_EXECUTE = 0x2B
    [<Literal>]
    let VK_SNAPSHOT = 0x2C
    [<Literal>]
    let VK_INSERT = 0x2D
    [<Literal>]
    let VK_DELETE = 0x2E
    [<Literal>]
    let VK_HELP = 0x2F
    [<Literal>]
    let VK_0 = 0x30
    [<Literal>]
    let VK_1 = 0x31
    [<Literal>]
    let VK_2 = 0x32
    [<Literal>]
    let VK_3 = 0x33
    [<Literal>]
    let VK_4 = 0x34
    [<Literal>]
    let VK_5 = 0x35
    [<Literal>]
    let VK_6 = 0x36
    [<Literal>]
    let VK_7 = 0x37
    [<Literal>]
    let VK_8 = 0x38
    [<Literal>]
    let VK_9 = 0x39
    [<Literal>]
    let VK_A = 0x41
    [<Literal>]
    let VK_B = 0x42
    [<Literal>]
    let VK_C = 0x43
    [<Literal>]
    let VK_D = 0x44
    [<Literal>]
    let VK_E = 0x45
    [<Literal>]
    let VK_F = 0x46
    [<Literal>]
    let VK_G = 0x47
    [<Literal>]
    let VK_H = 0x48
    [<Literal>]
    let VK_I = 0x49
    [<Literal>]
    let VK_J = 0x4A
    [<Literal>]
    let VK_K = 0x4B
    [<Literal>]
    let VK_L = 0x4C
    [<Literal>]
    let VK_M = 0x4D
    [<Literal>]
    let VK_N = 0x4E
    [<Literal>]
    let VK_O = 0x4F
    [<Literal>]
    let VK_P = 0x50
    [<Literal>]
    let VK_Q = 0x51
    [<Literal>]
    let VK_R = 0x52
    [<Literal>]
    let VK_S = 0x53
    [<Literal>]
    let VK_T = 0x54
    [<Literal>]
    let VK_U = 0x55
    [<Literal>]
    let VK_V = 0x56
    [<Literal>]
    let VK_W = 0x57
    [<Literal>]
    let VK_X = 0x58
    [<Literal>]
    let VK_Y = 0x59
    [<Literal>]
    let VK_Z = 0x5A
    [<Literal>]
    let VK_LWIN = 0x5B
    [<Literal>]
    let VK_RWIN = 0x5C
    [<Literal>]
    let VK_APPS = 0x5D
    [<Literal>]
    let VK_POWER = 0x5E
    [<Literal>]
    let VK_SLEEP = 0x5F
    [<Literal>]
    let VK_NUMPAD0 = 0x60
    [<Literal>]
    let VK_NUMPAD1 = 0x61
    [<Literal>]
    let VK_NUMPAD2 = 0x62
    [<Literal>]
    let VK_NUMPAD3 = 0x63
    [<Literal>]
    let VK_NUMPAD4 = 0x64
    [<Literal>]
    let VK_NUMPAD5 = 0x65
    [<Literal>]
    let VK_NUMPAD6 = 0x66
    [<Literal>]
    let VK_NUMPAD7 = 0x67
    [<Literal>]
    let VK_NUMPAD8 = 0x68
    [<Literal>]
    let VK_NUMPAD9 = 0x69
    [<Literal>]
    let VK_MULTIPLY = 0x6A
    [<Literal>]
    let VK_ADD = 0x6B
    [<Literal>]
    let VK_SEPARATOR = 0x6C
    [<Literal>]
    let VK_SUBTRACT = 0x6D
    [<Literal>]
    let VK_DECIMAL = 0x6E
    [<Literal>]
    let VK_DIVIDE = 0x6F
    [<Literal>]
    let VK_F1 = 0x70
    [<Literal>]
    let VK_F2 = 0x71
    [<Literal>]
    let VK_F3 = 0x72
    [<Literal>]
    let VK_F4 = 0x73
    [<Literal>]
    let VK_F5 = 0x74
    [<Literal>]
    let VK_F6 = 0x75
    [<Literal>]
    let VK_F7 = 0x76
    [<Literal>]
    let VK_F8 = 0x77
    [<Literal>]
    let VK_F9 = 0x78
    [<Literal>]
    let VK_F10 = 0x79
    [<Literal>]
    let VK_F11 = 0x7A
    [<Literal>]
    let VK_F12 = 0x7B
    [<Literal>]
    let VK_F13 = 0x7C
    [<Literal>]
    let VK_F14 = 0x7D
    [<Literal>]
    let VK_F15 = 0x7E
    [<Literal>]
    let VK_F16 = 0x7F
    [<Literal>]
    let VK_F17 = 0x80
    [<Literal>]
    let VK_F18 = 0x81
    [<Literal>]
    let VK_F19 = 0x82
    [<Literal>]
    let VK_F20 = 0x83
    [<Literal>]
    let VK_F21 = 0x84
    [<Literal>]
    let VK_F22 = 0x85
    [<Literal>]
    let VK_F23 = 0x86
    [<Literal>]
    let VK_F24 = 0x87
    [<Literal>]
    let VK_NUMLOCK = 0x90
    [<Literal>]
    let VK_SCROLL = 0x91
    [<Literal>]
    let VK_RSHIFT = 0xA1
    [<Literal>]
    let VK_BROWSER_BACK = 0xA6
    [<Literal>]
    let VK_BROWSER_FORWARD = 0xA7
    [<Literal>]
    let VK_BROWSER_REFRESH = 0xA8
    [<Literal>]
    let VK_BROWSER_STOP = 0xA9
    [<Literal>]
    let VK_BROWSER_SEARCH = 0xAA
    [<Literal>]
    let VK_BROWSER_FAVORITES = 0xAB
    [<Literal>]
    let VK_BROWSER_HOME = 0xAC
    [<Literal>]
    let VK_VOLUME_MUTE = 0xAD
    [<Literal>]
    let VK_VOLUME_DOWN = 0xAE
    [<Literal>]
    let VK_VOLUME_UP = 0xAF
    [<Literal>]
    let VK_MEDIA_NEXT_TRACK = 0xB0
    [<Literal>]
    let VK_MEDIA_PREV_TRACK = 0xB1
    [<Literal>]
    let VK_MEDIA_STOP = 0xB2
    [<Literal>]
    let VK_MEDIA_PLAY_PAUSE = 0xB3
    [<Literal>]
    let VK_LAUNCH_MAIL = 0xB4
    [<Literal>]
    let VK_LAUNCH_MEDIA_SELECT = 0xB5
    [<Literal>]
    let VK_LAUNCH_APP1 = 0xB6
    [<Literal>]
    let VK_LAUNCH_APP2 = 0xB7
    [<Literal>]
    let VK_PROCESSKEY = 0xE5
    [<Literal>]
    let VK_PACKET = 0xE7
    [<Literal>]
    let VK_ATTN = 0xF6
    [<Literal>]
    let VK_CRSEL = 0xF7
    [<Literal>]
    let VK_EXSEL = 0xF8
    [<Literal>]
    let VK_EREOF = 0xF9
    [<Literal>]
    let VK_PLAY = 0xFA
    [<Literal>]
    let VK_ZOOM = 0xFB
    [<Literal>]
    let VK_NONAME = 0xFC
    [<Literal>]
    let VK_PA1 = 0xFD
    [<Literal>]
    let VK_OEM_CLEAR = 0xFE
    [<Literal>]
    let VK_TAB = 0x09
    [<Literal>]
    let VK_SHIFT = 0x10
    [<Literal>]
    let VK_CONTROL = 0x11
    [<Literal>]
    let VK_MENU = 0x12
    [<Literal>]
    let WS_EX_NOACTIVATE = 0x08000000
    [<Literal>]
    let VK_LSHIFT = 0xA0
    [<Literal>]
    let VK_RMENU = 0xA5
    [<Literal>]
    let VK_LMENU = 0xA4
    [<Literal>]
    let VK_LCONTROL = 0xA2
    [<Literal>]
    let VK_RCONTROL = 0xA3
    [<Literal>]
    let VK_LBUTTON = 0x01
    [<Literal>]
    let VK_RBUTTON = 0x02
    [<Literal>]
    let VK_MBUTTON = 0x04
    [<Literal>]
    let VK_XBUTTON1 = 0x05
    [<Literal>]
    let VK_XBUTTON2 = 0x06
    [<Literal>]
    let VK_OEM_1 = 0xBA
    [<Literal>]
    let VK_OEM_PLUS = 0xBB
    [<Literal>]
    let VK_OEM_COMMA = 0xBC
    [<Literal>]
    let VK_OEM_MINUS = 0xBD
    [<Literal>]
    let VK_OEM_PERIOD = 0xBE
    [<Literal>]
    let VK_OEM_2 = 0xBF
    [<Literal>]
    let VK_OEM_3 = 0xC0
    [<Literal>]
    let VK_C1 = 0xC1   // Brazilian ABNT_C1 key (not defined in winuser.h).
    [<Literal>]
    let VK_C2 = 0xC2   // Brazilian ABNT_C2 key (not defined in winuser.h).
    [<Literal>]
    let VK_OEM_4 = 0xDB
    [<Literal>]
    let VK_OEM_5 = 0xDC
    [<Literal>]
    let VK_OEM_6 = 0xDD
    [<Literal>]
    let VK_OEM_7 = 0xDE
    [<Literal>]
    let VK_OEM_8 = 0xDF
    [<Literal>]
    let VK_OEM_AX = 0xE1
    [<Literal>]
    let VK_OEM_102 = 0xE2
    [<Literal>]
    let VK_OEM_RESET = 0xE9
    [<Literal>]
    let VK_OEM_JUMP = 0xEA
    [<Literal>]
    let VK_OEM_PA1 = 0xEB
    [<Literal>]
    let VK_OEM_PA2 = 0xEC
    [<Literal>]
    let VK_OEM_PA3 = 0xED
    [<Literal>]
    let VK_OEM_WSCTRL = 0xEE
    [<Literal>]
    let VK_OEM_CUSEL = 0xEF
    [<Literal>]
    let VK_OEM_ATTN = 0xF0
    [<Literal>]
    let VK_OEM_FINISH = 0xF1
    [<Literal>]
    let VK_OEM_COPY = 0xF2
    [<Literal>]
    let VK_OEM_AUTO = 0xF3
    [<Literal>]
    let VK_OEM_ENLW = 0xF4
    [<Literal>]
    let VK_OEM_BACKTAB = 0xF5

module KeyConverter =
    
    let keyDict =
        Dictionary.ofList [
            VirtualKeys.VK_CANCEL, Keys.Cancel
            VirtualKeys.VK_BACK, Keys.Back
            VirtualKeys.VK_TAB, Keys.Tab
            VirtualKeys.VK_CLEAR, Keys.Clear
            VirtualKeys.VK_RETURN, Keys.Return
            VirtualKeys.VK_PAUSE, Keys.Pause
            VirtualKeys.VK_CAPITAL, Keys.Capital
            VirtualKeys.VK_KANA, Keys.KanaMode
            VirtualKeys.VK_JUNJA, Keys.JunjaMode
            VirtualKeys.VK_FINAL, Keys.FinalMode
            VirtualKeys.VK_KANJI, Keys.KanjiMode
            VirtualKeys.VK_ESCAPE, Keys.Escape
            VirtualKeys.VK_CONVERT, Keys.ImeConvert
            VirtualKeys.VK_NONCONVERT, Keys.ImeNonConvert
            VirtualKeys.VK_ACCEPT, Keys.ImeAccept
            VirtualKeys.VK_MODECHANGE, Keys.ImeModeChange
            VirtualKeys.VK_SPACE, Keys.Space
            VirtualKeys.VK_PRIOR, Keys.Prior
            VirtualKeys.VK_NEXT, Keys.Next
            VirtualKeys.VK_END, Keys.End
            VirtualKeys.VK_HOME, Keys.Home
            VirtualKeys.VK_LEFT, Keys.Left
            VirtualKeys.VK_UP, Keys.Up
            VirtualKeys.VK_RIGHT, Keys.Right
            VirtualKeys.VK_DOWN, Keys.Down
            VirtualKeys.VK_SELECT, Keys.Select
            VirtualKeys.VK_PRINT, Keys.Print
            VirtualKeys.VK_EXECUTE, Keys.Execute
            VirtualKeys.VK_SNAPSHOT, Keys.Snapshot
            VirtualKeys.VK_INSERT, Keys.Insert
            VirtualKeys.VK_DELETE, Keys.Delete
            VirtualKeys.VK_HELP, Keys.Help
            VirtualKeys.VK_0, Keys.D0
            VirtualKeys.VK_1, Keys.D1
            VirtualKeys.VK_2, Keys.D2
            VirtualKeys.VK_3, Keys.D3
            VirtualKeys.VK_4, Keys.D4
            VirtualKeys.VK_5, Keys.D5
            VirtualKeys.VK_6, Keys.D6
            VirtualKeys.VK_7, Keys.D7
            VirtualKeys.VK_8, Keys.D8
            VirtualKeys.VK_9, Keys.D9
            VirtualKeys.VK_A, Keys.A
            VirtualKeys.VK_B, Keys.B
            VirtualKeys.VK_C, Keys.C
            VirtualKeys.VK_D, Keys.D
            VirtualKeys.VK_E, Keys.E
            VirtualKeys.VK_F, Keys.F
            VirtualKeys.VK_G, Keys.G
            VirtualKeys.VK_H, Keys.H
            VirtualKeys.VK_I, Keys.I
            VirtualKeys.VK_J, Keys.J
            VirtualKeys.VK_K, Keys.K
            VirtualKeys.VK_L, Keys.L
            VirtualKeys.VK_M, Keys.M
            VirtualKeys.VK_N, Keys.N
            VirtualKeys.VK_O, Keys.O
            VirtualKeys.VK_P, Keys.P
            VirtualKeys.VK_Q, Keys.Q
            VirtualKeys.VK_R, Keys.R
            VirtualKeys.VK_S, Keys.S
            VirtualKeys.VK_T, Keys.T
            VirtualKeys.VK_U, Keys.U
            VirtualKeys.VK_V, Keys.V
            VirtualKeys.VK_W, Keys.W
            VirtualKeys.VK_X, Keys.X
            VirtualKeys.VK_Y, Keys.Y
            VirtualKeys.VK_Z, Keys.Z
            VirtualKeys.VK_LWIN, Keys.LWin
            VirtualKeys.VK_RWIN, Keys.RWin
            VirtualKeys.VK_APPS, Keys.Apps
            VirtualKeys.VK_SLEEP, Keys.Sleep
            VirtualKeys.VK_NUMPAD0, Keys.NumPad0
            VirtualKeys.VK_NUMPAD1, Keys.NumPad1
            VirtualKeys.VK_NUMPAD2, Keys.NumPad2
            VirtualKeys.VK_NUMPAD3, Keys.NumPad3
            VirtualKeys.VK_NUMPAD4, Keys.NumPad4
            VirtualKeys.VK_NUMPAD5, Keys.NumPad5
            VirtualKeys.VK_NUMPAD6, Keys.NumPad6
            VirtualKeys.VK_NUMPAD7, Keys.NumPad7
            VirtualKeys.VK_NUMPAD8, Keys.NumPad8
            VirtualKeys.VK_NUMPAD9, Keys.NumPad9
            VirtualKeys.VK_MULTIPLY, Keys.Multiply
            VirtualKeys.VK_ADD, Keys.Add
            VirtualKeys.VK_SEPARATOR, Keys.Separator
            VirtualKeys.VK_SUBTRACT, Keys.Subtract
            VirtualKeys.VK_DECIMAL, Keys.Decimal
            VirtualKeys.VK_DIVIDE, Keys.Divide
            VirtualKeys.VK_F1, Keys.F1
            VirtualKeys.VK_F2, Keys.F2
            VirtualKeys.VK_F3, Keys.F3
            VirtualKeys.VK_F4, Keys.F4
            VirtualKeys.VK_F5, Keys.F5
            VirtualKeys.VK_F6, Keys.F6
            VirtualKeys.VK_F7, Keys.F7
            VirtualKeys.VK_F8, Keys.F8
            VirtualKeys.VK_F9, Keys.F9
            VirtualKeys.VK_F10, Keys.F10
            VirtualKeys.VK_F11, Keys.F11
            VirtualKeys.VK_F12, Keys.F12
            VirtualKeys.VK_F13, Keys.F13
            VirtualKeys.VK_F14, Keys.F14
            VirtualKeys.VK_F15, Keys.F15
            VirtualKeys.VK_F16, Keys.F16
            VirtualKeys.VK_F17, Keys.F17
            VirtualKeys.VK_F18, Keys.F18
            VirtualKeys.VK_F19, Keys.F19
            VirtualKeys.VK_F20, Keys.F20
            VirtualKeys.VK_F21, Keys.F21
            VirtualKeys.VK_F22, Keys.F22
            VirtualKeys.VK_F23, Keys.F23
            VirtualKeys.VK_F24, Keys.F24
            VirtualKeys.VK_NUMLOCK, Keys.NumLock
            VirtualKeys.VK_SCROLL, Keys.Scroll
            VirtualKeys.VK_SHIFT, Keys.LeftShift
            VirtualKeys.VK_LSHIFT, Keys.LeftShift
            VirtualKeys.VK_RSHIFT, Keys.RightShift
            VirtualKeys.VK_CONTROL, Keys.LeftCtrl
            VirtualKeys.VK_LCONTROL, Keys.LeftCtrl
            VirtualKeys.VK_RCONTROL, Keys.RightCtrl
            VirtualKeys.VK_MENU, Keys.LeftAlt
            VirtualKeys.VK_LMENU, Keys.LeftAlt
            VirtualKeys.VK_RMENU, Keys.RightAlt
            VirtualKeys.VK_BROWSER_BACK, Keys.BrowserBack
            VirtualKeys.VK_BROWSER_FORWARD, Keys.BrowserForward
            VirtualKeys.VK_BROWSER_REFRESH, Keys.BrowserRefresh
            VirtualKeys.VK_BROWSER_STOP, Keys.BrowserStop
            VirtualKeys.VK_BROWSER_SEARCH, Keys.BrowserSearch
            VirtualKeys.VK_BROWSER_FAVORITES, Keys.BrowserFavorites
            VirtualKeys.VK_BROWSER_HOME, Keys.BrowserHome
            VirtualKeys.VK_VOLUME_MUTE, Keys.VolumeMute
            VirtualKeys.VK_VOLUME_DOWN, Keys.VolumeDown
            VirtualKeys.VK_VOLUME_UP, Keys.VolumeUp
            VirtualKeys.VK_MEDIA_NEXT_TRACK, Keys.MediaNextTrack
            VirtualKeys.VK_MEDIA_PREV_TRACK, Keys.MediaPreviousTrack
            VirtualKeys.VK_MEDIA_STOP, Keys.MediaStop
            VirtualKeys.VK_MEDIA_PLAY_PAUSE, Keys.MediaPlayPause
            VirtualKeys.VK_LAUNCH_MAIL, Keys.LaunchMail
            VirtualKeys.VK_LAUNCH_MEDIA_SELECT, Keys.SelectMedia
            VirtualKeys.VK_LAUNCH_APP1, Keys.LaunchApplication1
            VirtualKeys.VK_LAUNCH_APP2, Keys.LaunchApplication2
            VirtualKeys.VK_OEM_1, Keys.OemSemicolon
            VirtualKeys.VK_OEM_PLUS, Keys.OemPlus
            VirtualKeys.VK_OEM_COMMA, Keys.OemComma
            VirtualKeys.VK_OEM_MINUS, Keys.OemMinus
            VirtualKeys.VK_OEM_PERIOD, Keys.OemPeriod
            VirtualKeys.VK_OEM_2, Keys.OemQuestion
            VirtualKeys.VK_OEM_3, Keys.OemTilde
            VirtualKeys.VK_C1, Keys.AbntC1
            VirtualKeys.VK_C2, Keys.AbntC2
            VirtualKeys.VK_OEM_4, Keys.OemOpenBrackets
            VirtualKeys.VK_OEM_5, Keys.OemPipe
            VirtualKeys.VK_OEM_6, Keys.OemCloseBrackets
            VirtualKeys.VK_OEM_7, Keys.OemQuotes
            VirtualKeys.VK_OEM_8, Keys.Oem8
            VirtualKeys.VK_OEM_102, Keys.OemBackslash
            VirtualKeys.VK_PROCESSKEY, Keys.ImeProcessed
            VirtualKeys.VK_OEM_ATTN, Keys.OemAttn
            VirtualKeys.VK_OEM_FINISH, Keys.OemFinish
            VirtualKeys.VK_OEM_COPY, Keys.OemCopy
            VirtualKeys.VK_OEM_AUTO, Keys.OemAuto
            VirtualKeys.VK_OEM_ENLW, Keys.OemEnlw
            VirtualKeys.VK_OEM_BACKTAB, Keys.OemBackTab
            VirtualKeys.VK_ATTN, Keys.Attn
            VirtualKeys.VK_CRSEL, Keys.CrSel
            VirtualKeys.VK_EXSEL, Keys.ExSel
            VirtualKeys.VK_EREOF, Keys.EraseEof
            VirtualKeys.VK_PLAY, Keys.Play
            VirtualKeys.VK_ZOOM, Keys.Zoom
            VirtualKeys.VK_NONAME, Keys.NoName
            VirtualKeys.VK_PA1, Keys.Pa1
            VirtualKeys.VK_OEM_CLEAR, Keys.OemClear 

        ]

    let virtualKeyDict =
        Dictionary.ofList [
            Keys.Cancel, VirtualKeys.VK_CANCEL
            Keys.Back, VirtualKeys.VK_BACK
            Keys.Tab, VirtualKeys.VK_TAB
            Keys.Clear, VirtualKeys.VK_CLEAR
            Keys.Return, VirtualKeys.VK_RETURN
            Keys.Pause, VirtualKeys.VK_PAUSE
            Keys.Capital, VirtualKeys.VK_CAPITAL
            Keys.KanaMode, VirtualKeys.VK_KANA
            Keys.JunjaMode, VirtualKeys.VK_JUNJA
            Keys.FinalMode, VirtualKeys.VK_FINAL
            Keys.KanjiMode, VirtualKeys.VK_KANJI
            Keys.Escape, VirtualKeys.VK_ESCAPE
            Keys.ImeConvert, VirtualKeys.VK_CONVERT
            Keys.ImeNonConvert, VirtualKeys.VK_NONCONVERT
            Keys.ImeAccept, VirtualKeys.VK_ACCEPT
            Keys.ImeModeChange, VirtualKeys.VK_MODECHANGE
            Keys.Space, VirtualKeys.VK_SPACE
            Keys.Prior, VirtualKeys.VK_PRIOR
            Keys.Next, VirtualKeys.VK_NEXT
            Keys.End, VirtualKeys.VK_END
            Keys.Home, VirtualKeys.VK_HOME
            Keys.Left, VirtualKeys.VK_LEFT
            Keys.Up, VirtualKeys.VK_UP
            Keys.Right, VirtualKeys.VK_RIGHT
            Keys.Down, VirtualKeys.VK_DOWN
            Keys.Select, VirtualKeys.VK_SELECT
            Keys.Print, VirtualKeys.VK_PRINT
            Keys.Execute, VirtualKeys.VK_EXECUTE
            Keys.Snapshot, VirtualKeys.VK_SNAPSHOT
            Keys.Insert, VirtualKeys.VK_INSERT
            Keys.Delete, VirtualKeys.VK_DELETE
            Keys.Help, VirtualKeys.VK_HELP
            Keys.D0, VirtualKeys.VK_0
            Keys.D1, VirtualKeys.VK_1
            Keys.D2, VirtualKeys.VK_2
            Keys.D3, VirtualKeys.VK_3
            Keys.D4, VirtualKeys.VK_4
            Keys.D5, VirtualKeys.VK_5
            Keys.D6, VirtualKeys.VK_6
            Keys.D7, VirtualKeys.VK_7
            Keys.D8, VirtualKeys.VK_8
            Keys.D9, VirtualKeys.VK_9
            Keys.A, VirtualKeys.VK_A
            Keys.B, VirtualKeys.VK_B
            Keys.C, VirtualKeys.VK_C
            Keys.D, VirtualKeys.VK_D
            Keys.E, VirtualKeys.VK_E
            Keys.F, VirtualKeys.VK_F
            Keys.G, VirtualKeys.VK_G
            Keys.H, VirtualKeys.VK_H
            Keys.I, VirtualKeys.VK_I
            Keys.J, VirtualKeys.VK_J
            Keys.K, VirtualKeys.VK_K
            Keys.L, VirtualKeys.VK_L
            Keys.M, VirtualKeys.VK_M
            Keys.N, VirtualKeys.VK_N
            Keys.O, VirtualKeys.VK_O
            Keys.P, VirtualKeys.VK_P
            Keys.Q, VirtualKeys.VK_Q
            Keys.R, VirtualKeys.VK_R
            Keys.S, VirtualKeys.VK_S
            Keys.T, VirtualKeys.VK_T
            Keys.U, VirtualKeys.VK_U
            Keys.V, VirtualKeys.VK_V
            Keys.W, VirtualKeys.VK_W
            Keys.X, VirtualKeys.VK_X
            Keys.Y, VirtualKeys.VK_Y
            Keys.Z, VirtualKeys.VK_Z
            Keys.LWin, VirtualKeys.VK_LWIN
            Keys.RWin, VirtualKeys.VK_RWIN
            Keys.Apps, VirtualKeys.VK_APPS
            Keys.Sleep, VirtualKeys.VK_SLEEP
            Keys.NumPad0, VirtualKeys.VK_NUMPAD0
            Keys.NumPad1, VirtualKeys.VK_NUMPAD1
            Keys.NumPad2, VirtualKeys.VK_NUMPAD2
            Keys.NumPad3, VirtualKeys.VK_NUMPAD3
            Keys.NumPad4, VirtualKeys.VK_NUMPAD4
            Keys.NumPad5, VirtualKeys.VK_NUMPAD5
            Keys.NumPad6, VirtualKeys.VK_NUMPAD6
            Keys.NumPad7, VirtualKeys.VK_NUMPAD7
            Keys.NumPad8, VirtualKeys.VK_NUMPAD8
            Keys.NumPad9, VirtualKeys.VK_NUMPAD9
            Keys.Multiply, VirtualKeys.VK_MULTIPLY
            Keys.Add, VirtualKeys.VK_ADD
            Keys.Separator, VirtualKeys.VK_SEPARATOR
            Keys.Subtract, VirtualKeys.VK_SUBTRACT
            Keys.Decimal, VirtualKeys.VK_DECIMAL
            Keys.Divide, VirtualKeys.VK_DIVIDE
            Keys.F1, VirtualKeys.VK_F1
            Keys.F2, VirtualKeys.VK_F2
            Keys.F3, VirtualKeys.VK_F3
            Keys.F4, VirtualKeys.VK_F4
            Keys.F5, VirtualKeys.VK_F5
            Keys.F6, VirtualKeys.VK_F6
            Keys.F7, VirtualKeys.VK_F7
            Keys.F8, VirtualKeys.VK_F8
            Keys.F9, VirtualKeys.VK_F9
            Keys.F10, VirtualKeys.VK_F10
            Keys.F11, VirtualKeys.VK_F11
            Keys.F12, VirtualKeys.VK_F12
            Keys.F13, VirtualKeys.VK_F13
            Keys.F14, VirtualKeys.VK_F14
            Keys.F15, VirtualKeys.VK_F15
            Keys.F16, VirtualKeys.VK_F16
            Keys.F17, VirtualKeys.VK_F17
            Keys.F18, VirtualKeys.VK_F18
            Keys.F19, VirtualKeys.VK_F19
            Keys.F20, VirtualKeys.VK_F20
            Keys.F21, VirtualKeys.VK_F21
            Keys.F22, VirtualKeys.VK_F22
            Keys.F23, VirtualKeys.VK_F23
            Keys.F24, VirtualKeys.VK_F24
            Keys.NumLock, VirtualKeys.VK_NUMLOCK
            Keys.Scroll, VirtualKeys.VK_SCROLL
            Keys.LeftShift, VirtualKeys.VK_SHIFT
            Keys.RightShift, VirtualKeys.VK_RSHIFT
            Keys.LeftCtrl, VirtualKeys.VK_CONTROL
            Keys.RightCtrl, VirtualKeys.VK_RCONTROL
            Keys.LeftAlt, VirtualKeys.VK_MENU
            Keys.RightAlt, VirtualKeys.VK_RMENU
            Keys.BrowserBack, VirtualKeys.VK_BROWSER_BACK
            Keys.BrowserForward, VirtualKeys.VK_BROWSER_FORWARD
            Keys.BrowserRefresh, VirtualKeys.VK_BROWSER_REFRESH
            Keys.BrowserStop, VirtualKeys.VK_BROWSER_STOP
            Keys.BrowserSearch, VirtualKeys.VK_BROWSER_SEARCH
            Keys.BrowserFavorites, VirtualKeys.VK_BROWSER_FAVORITES
            Keys.BrowserHome, VirtualKeys.VK_BROWSER_HOME
            Keys.VolumeMute, VirtualKeys.VK_VOLUME_MUTE
            Keys.VolumeDown, VirtualKeys.VK_VOLUME_DOWN
            Keys.VolumeUp, VirtualKeys.VK_VOLUME_UP
            Keys.MediaNextTrack, VirtualKeys.VK_MEDIA_NEXT_TRACK
            Keys.MediaPreviousTrack, VirtualKeys.VK_MEDIA_PREV_TRACK
            Keys.MediaStop, VirtualKeys.VK_MEDIA_STOP
            Keys.MediaPlayPause, VirtualKeys.VK_MEDIA_PLAY_PAUSE
            Keys.LaunchMail, VirtualKeys.VK_LAUNCH_MAIL
            Keys.SelectMedia, VirtualKeys.VK_LAUNCH_MEDIA_SELECT
            Keys.LaunchApplication1, VirtualKeys.VK_LAUNCH_APP1
            Keys.LaunchApplication2, VirtualKeys.VK_LAUNCH_APP2
            Keys.OemSemicolon, VirtualKeys.VK_OEM_1
            Keys.OemPlus, VirtualKeys.VK_OEM_PLUS
            Keys.OemComma, VirtualKeys.VK_OEM_COMMA
            Keys.OemMinus, VirtualKeys.VK_OEM_MINUS
            Keys.OemPeriod, VirtualKeys.VK_OEM_PERIOD
            Keys.OemQuestion, VirtualKeys.VK_OEM_2
            Keys.OemTilde, VirtualKeys.VK_OEM_3
            Keys.AbntC1, VirtualKeys.VK_C1
            Keys.AbntC2, VirtualKeys.VK_C2
            Keys.OemOpenBrackets, VirtualKeys.VK_OEM_4
            Keys.OemPipe, VirtualKeys.VK_OEM_5
            Keys.OemCloseBrackets, VirtualKeys.VK_OEM_6
            Keys.OemQuotes, VirtualKeys.VK_OEM_7
            Keys.Oem8, VirtualKeys.VK_OEM_8
            Keys.OemBackslash, VirtualKeys.VK_OEM_102
            Keys.ImeProcessed, VirtualKeys.VK_PROCESSKEY
            Keys.OemAttn, VirtualKeys.VK_OEM_ATTN
            Keys.OemFinish, VirtualKeys.VK_OEM_FINISH
            Keys.OemCopy, VirtualKeys.VK_OEM_COPY
            Keys.OemAuto, VirtualKeys.VK_OEM_AUTO
            Keys.OemEnlw, VirtualKeys.VK_OEM_ENLW
            Keys.OemBackTab, VirtualKeys.VK_OEM_BACKTAB
            Keys.Attn, VirtualKeys.VK_ATTN
            Keys.CrSel, VirtualKeys.VK_CRSEL
            Keys.ExSel, VirtualKeys.VK_EXSEL
            Keys.EraseEof, VirtualKeys.VK_EREOF
            Keys.Play, VirtualKeys.VK_PLAY
            Keys.Zoom, VirtualKeys.VK_ZOOM
            Keys.NoName, VirtualKeys.VK_NONAME
            Keys.Pa1, VirtualKeys.VK_PA1
            Keys.OemClear , VirtualKeys.VK_OEM_CLEAR
        ]

    let keyFromVirtualKey (k : int) =
        match keyDict.TryGetValue k with
            | (true, k) -> k
            | _ -> Keys.None

    let virtualKeyFromKey (k : Keys) =
        match virtualKeyDict.TryGetValue k with
            | (true, k) -> k
            | _ -> 0