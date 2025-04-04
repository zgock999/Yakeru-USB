// プレハブは直接テキストで提供できないため、構築方法を説明します。
// 以下の階層構造に従ってGameObjectを作成し、最後にPrefabとして保存してください。

// WizardUI (Canvas)
// ├── TitlePanel
// │   ├── Logo
// │   ├── Title
// │   ├── Subtitle
// │   └── StartButton
// ├── ISOSelectionPanel
// │   ├── StepTitle
// │   ├── ISOListScrollView
// │   │   └── Viewport
// │   │       └── Content
// │   ├── SelectedISOText 
// │   └── RefreshButton
// ├── USBSelectionPanel
// │   ├── StepTitle
// │   ├── USBListScrollView
// │   │   └── Viewport
// │   │       └── Content
// │   ├── SelectedUSBText
// │   └── RefreshButton
// ├── ConfirmationPanel
// │   ├── StepTitle
// │   ├── InfoContainer
// │   │   ├── ISOInfoGroup
// │   │   │   ├── Label
// │   │   │   ├── ISONameText
// │   │   │   └── ISOSizeText
// │   │   └── DeviceInfoGroup
// │   │       ├── Label
// │   │       ├── DeviceNameText
// │   │       └── DeviceSizeText
// │   ├── WarningText
// │   └── WriteButton
// ├── WritingPanel
// │   ├── StepTitle
// │   ├── ProgressBar
// │   ├── ProgressText
// │   └── StatusText
// ├── CompletionPanel
// │   ├── CompletionIcon
// │   ├── CompletionMessageText
// │   ├── FinishButton
// │   ├── ConfirmQuitButton
// │   └── CancelButton
// ├── NavigationButtons
// │   ├── BackButton
// │   ├── NextButton
// │   └── QuitButton
// └── UIWizard (Script)
