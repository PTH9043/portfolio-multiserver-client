#nullable enable
namespace ChatClient;

partial class Form1
{
    private System.ComponentModel.IContainer? components = null;

    private Panel _topPanel = null!;
    private Label _hostLabel = null!;
    private TextBox _hostTextBox = null!;
    private Label _portLabel = null!;
    private NumericUpDown _portInput = null!;
    private Label _nicknameLabel = null!;
    private TextBox _nicknameTextBox = null!;
    private Button _connectButton = null!;
    private Label _statusLabel = null!;

    private SplitContainer _mainSplit = null!;

    private GroupBox _chatGroupBox = null!;
    private RichTextBox _chatLogBox = null!;
    private TableLayoutPanel _chatInputPanel = null!;
    private TextBox _chatInputBox = null!;
    private Label _chatLengthLabel = null!;
    private Button _sendButton = null!;

    private GroupBox _usersGroupBox = null!;
    private TableLayoutPanel _usersTopPanel = null!;
    private Label _userCountLabel = null!;
    private Button _refreshUsersButton = null!;
    private ListBox _usersListBox = null!;
    private TableLayoutPanel _pagingPanel = null!;
    private Button _prevPageButton = null!;
    private Label _pageInfoLabel = null!;
    private Button _nextPageButton = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        _topPanel = new Panel();
        _hostLabel = new Label();
        _hostTextBox = new TextBox();
        _portLabel = new Label();
        _portInput = new NumericUpDown();
        _nicknameLabel = new Label();
        _nicknameTextBox = new TextBox();
        _connectButton = new Button();
        _statusLabel = new Label();

        _mainSplit = new SplitContainer();
        _chatGroupBox = new GroupBox();
        _chatLogBox = new RichTextBox();
        _chatInputPanel = new TableLayoutPanel();
        _chatInputBox = new TextBox();
        _chatLengthLabel = new Label();
        _sendButton = new Button();

        _usersGroupBox = new GroupBox();
        _usersTopPanel = new TableLayoutPanel();
        _userCountLabel = new Label();
        _refreshUsersButton = new Button();
        _usersListBox = new ListBox();
        _pagingPanel = new TableLayoutPanel();
        _prevPageButton = new Button();
        _pageInfoLabel = new Label();
        _nextPageButton = new Button();

        ((System.ComponentModel.ISupportInitialize)_portInput).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_mainSplit).BeginInit();
        _mainSplit.Panel1.SuspendLayout();
        _mainSplit.Panel2.SuspendLayout();
        _mainSplit.SuspendLayout();
        _topPanel.SuspendLayout();
        _chatGroupBox.SuspendLayout();
        _chatInputPanel.SuspendLayout();
        _usersGroupBox.SuspendLayout();
        _usersTopPanel.SuspendLayout();
        _pagingPanel.SuspendLayout();
        SuspendLayout();

        // Form1
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(980, 620);
        MinimumSize = new Size(780, 480);
        Font = new Font("Malgun Gothic", 9.5F, FontStyle.Regular);
        Text = "MultiServer 채팅 데모 클라이언트";

        // Top connection bar
        _topPanel.Dock = DockStyle.Top;
        _topPanel.Size = new Size(980, 52);
        _topPanel.Padding = new Padding(10, 10, 10, 8);
        _topPanel.BackColor = Color.FromArgb(242, 245, 249);

        _hostLabel.AutoSize = true;
        _hostLabel.Text = "서버";
        _hostLabel.Location = new Point(10, 16);

        _hostTextBox.Text = "127.0.0.1";
        _hostTextBox.Location = new Point(48, 12);
        _hostTextBox.Size = new Size(120, 26);

        _portLabel.AutoSize = true;
        _portLabel.Text = "포트";
        _portLabel.Location = new Point(178, 16);

        _portInput.Minimum = 1;
        _portInput.Maximum = 65535;
        _portInput.Value = 7777;
        _portInput.Location = new Point(216, 12);
        _portInput.Size = new Size(74, 26);

        _nicknameLabel.AutoSize = true;
        _nicknameLabel.Text = "닉네임";
        _nicknameLabel.Location = new Point(302, 16);

        _nicknameTextBox.PlaceholderText = "2~12자 한글/영문/숫자";
        _nicknameTextBox.MaxLength = 12;
        _nicknameTextBox.Location = new Point(352, 12);
        _nicknameTextBox.Size = new Size(160, 26);

        _connectButton.Text = "접속 / 로그인";
        _connectButton.Location = new Point(522, 10);
        _connectButton.Size = new Size(116, 30);

        _statusLabel.AutoSize = true;
        _statusLabel.Text = "상태: 미연결";
        _statusLabel.ForeColor = Color.FromArgb(71, 85, 105);
        _statusLabel.Location = new Point(650, 16);

        _topPanel.Controls.Add(_hostLabel);
        _topPanel.Controls.Add(_hostTextBox);
        _topPanel.Controls.Add(_portLabel);
        _topPanel.Controls.Add(_portInput);
        _topPanel.Controls.Add(_nicknameLabel);
        _topPanel.Controls.Add(_nicknameTextBox);
        _topPanel.Controls.Add(_connectButton);
        _topPanel.Controls.Add(_statusLabel);

        // Main SplitContainer
        _mainSplit.Dock = DockStyle.Fill;
        _mainSplit.Size = new Size(980, 568);
        _mainSplit.Panel1MinSize = 420;
        _mainSplit.Panel2MinSize = 240;
        _mainSplit.SplitterDistance = 670;

        // Left: Chat panel
        _chatGroupBox.Dock = DockStyle.Fill;
        _chatGroupBox.Padding = new Padding(8);
        _chatGroupBox.Text = "실시간 채팅 (기본 방 #1)";

        _chatLogBox.Dock = DockStyle.Fill;
        _chatLogBox.ReadOnly = true;
        _chatLogBox.BorderStyle = BorderStyle.FixedSingle;
        _chatLogBox.BackColor = Color.White;
        _chatLogBox.DetectUrls = false;

        _chatInputPanel.Dock = DockStyle.Bottom;
        _chatInputPanel.Height = 46;
        _chatInputPanel.Padding = new Padding(0, 8, 0, 0);
        _chatInputPanel.ColumnCount = 3;
        _chatInputPanel.RowCount = 1;
        _chatInputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _chatInputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _chatInputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _chatInputPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _chatInputBox.Dock = DockStyle.Fill;
        _chatInputBox.Margin = new Padding(0, 4, 6, 0);
        _chatInputBox.PlaceholderText = "메시지를 입력하세요 (최대 200자, Enter로 전송)";
        _chatInputBox.Enabled = false;

        _chatLengthLabel.AutoSize = true;
        _chatLengthLabel.MinimumSize = new Size(58, 0);
        _chatLengthLabel.Anchor = AnchorStyles.None;
        _chatLengthLabel.Margin = new Padding(0, 0, 6, 0);
        _chatLengthLabel.Text = "0 / 200";
        _chatLengthLabel.TextAlign = ContentAlignment.MiddleRight;
        _chatLengthLabel.ForeColor = Color.FromArgb(100, 116, 139);

        _sendButton.Anchor = AnchorStyles.None;
        _sendButton.Margin = new Padding(0);
        _sendButton.Text = "전송";
        _sendButton.Size = new Size(80, 30);
        _sendButton.Enabled = false;

        _chatInputPanel.Controls.Add(_chatInputBox, 0, 0);
        _chatInputPanel.Controls.Add(_chatLengthLabel, 1, 0);
        _chatInputPanel.Controls.Add(_sendButton, 2, 0);

        _chatGroupBox.Controls.Add(_chatLogBox);
        _chatGroupBox.Controls.Add(_chatInputPanel);
        _mainSplit.Panel1.Padding = new Padding(8);
        _mainSplit.Panel1.Controls.Add(_chatGroupBox);

        // Right: Room users paging panel
        _usersGroupBox.Dock = DockStyle.Fill;
        _usersGroupBox.Padding = new Padding(8);
        _usersGroupBox.Text = "방 접속자 (20명 페이징)";

        _usersTopPanel.Dock = DockStyle.Top;
        _usersTopPanel.Height = 36;
        _usersTopPanel.ColumnCount = 2;
        _usersTopPanel.RowCount = 1;
        _usersTopPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _usersTopPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _usersTopPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _userCountLabel.AutoSize = true;
        _userCountLabel.Anchor = AnchorStyles.Left;
        _userCountLabel.Margin = new Padding(2, 0, 6, 0);
        _userCountLabel.Text = "현재 인원: 0 / 1,000명";
        _userCountLabel.Font = new Font("Malgun Gothic", 9.5F, FontStyle.Bold);

        _refreshUsersButton.Anchor = AnchorStyles.Right;
        _refreshUsersButton.Margin = new Padding(0);
        _refreshUsersButton.Text = "새로고침";
        _refreshUsersButton.Size = new Size(78, 28);
        _refreshUsersButton.Enabled = false;

        _usersTopPanel.Controls.Add(_userCountLabel, 0, 0);
        _usersTopPanel.Controls.Add(_refreshUsersButton, 1, 0);

        _usersListBox.Dock = DockStyle.Fill;
        _usersListBox.IntegralHeight = false;

        _pagingPanel.Dock = DockStyle.Bottom;
        _pagingPanel.Height = 38;
        _pagingPanel.Padding = new Padding(0, 6, 0, 0);
        _pagingPanel.ColumnCount = 3;
        _pagingPanel.RowCount = 1;
        _pagingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _pagingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _pagingPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _pagingPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _prevPageButton.Anchor = AnchorStyles.Left;
        _prevPageButton.Margin = new Padding(0);
        _prevPageButton.Text = "◀ 이전";
        _prevPageButton.Size = new Size(72, 28);
        _prevPageButton.Enabled = false;

        _pageInfoLabel.Dock = DockStyle.Fill;
        _pageInfoLabel.Margin = new Padding(4, 0, 4, 0);
        _pageInfoLabel.Text = "0 / 0쪽";
        _pageInfoLabel.TextAlign = ContentAlignment.MiddleCenter;

        _nextPageButton.Anchor = AnchorStyles.Right;
        _nextPageButton.Margin = new Padding(0);
        _nextPageButton.Text = "다음 ▶";
        _nextPageButton.Size = new Size(72, 28);
        _nextPageButton.Enabled = false;

        _pagingPanel.Controls.Add(_prevPageButton, 0, 0);
        _pagingPanel.Controls.Add(_pageInfoLabel, 1, 0);
        _pagingPanel.Controls.Add(_nextPageButton, 2, 0);

        _usersGroupBox.Controls.Add(_usersListBox);
        _usersGroupBox.Controls.Add(_usersTopPanel);
        _usersGroupBox.Controls.Add(_pagingPanel);
        _mainSplit.Panel2.Padding = new Padding(0, 8, 8, 8);
        _mainSplit.Panel2.Controls.Add(_usersGroupBox);

        Controls.Add(_mainSplit);
        Controls.Add(_topPanel);

        ((System.ComponentModel.ISupportInitialize)_portInput).EndInit();
        _mainSplit.Panel1.ResumeLayout(false);
        _mainSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_mainSplit).EndInit();
        _mainSplit.ResumeLayout(false);
        _topPanel.ResumeLayout(false);
        _topPanel.PerformLayout();
        _chatGroupBox.ResumeLayout(false);
        _chatInputPanel.ResumeLayout(false);
        _chatInputPanel.PerformLayout();
        _usersGroupBox.ResumeLayout(false);
        _usersTopPanel.ResumeLayout(false);
        _usersTopPanel.PerformLayout();
        _pagingPanel.ResumeLayout(false);
        _pagingPanel.PerformLayout();
        ResumeLayout(false);
    }

    #endregion
}
