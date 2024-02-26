using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using ConsoleControlAPI;
using JackTheVideoRipper.extensions;

namespace ConsoleControl
{
    /// <summary>
    /// The console event handler is used for console events.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="args">The <see cref="ConsoleEventArgs"/> instance containing the event data.</param>
    public delegate void ConsoleEventHandler(object sender, ConsoleEventArgs args);

    /// <summary>
    /// The Console Control allows you to embed a basic console in your application.
    /// </summary>
    [ToolboxBitmap(typeof(Resfinder), "ConsoleControl.ConsoleControl.bmp")]
    public partial class ConsoleControl : UserControl
    {
        public const string FORMS_NEWLINE = "\r\n";

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleControl"/> class.
        /// </summary>
        public ConsoleControl()
        {
            //  Initialise the component.
            InitializeComponent();

            //  Show diagnostics disabled by default.
            ShowDiagnostics = false;

            //  Input enabled by default.
            IsInputEnabled = true;

            //  Disable special commands by default.
            SendKeyboardCommandsToProcess = false;

            //  Handle process events.
            ProcessInterface.OnProcessOutput += processInterface_OnProcessOutput;
            ProcessInterface.OnProcessError += processInterface_OnProcessError;
            ProcessInterface.OnProcessInput += processInterface_OnProcessInput;
            ProcessInterface.OnProcessExit += processInterface_OnProcessExit;

            //  Wait for key down messages on the rich text box.
            InternalRichTextBox!.KeyDown += richTextBoxConsole_KeyDown;
        }

        /// <summary>
        /// Handles the OnProcessError event of the processInterface control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        private void processInterface_OnProcessError(object sender, ProcessEventArgs args)
        {
            //  Write the output, in red
            WriteOutput(args.Content, _errorColor);

            //  Fire the output event.
            FireConsoleOutputEvent(args.Content);
        }

        /// <summary>
        /// Handles the OnProcessOutput event of the processInterface control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        private void processInterface_OnProcessOutput(object sender, ProcessEventArgs args)
        {
            //  Write the output, in white
            WriteOutput(args.Content, _primaryColor);

            //  Fire the output event.
            FireConsoleOutputEvent(args.Content);
        }

        /// <summary>
        /// Handles the OnProcessInput event of the processInterface control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        private void processInterface_OnProcessInput(object sender, ProcessEventArgs args)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Handles the OnProcessExit event of the processInterface control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        private void processInterface_OnProcessExit(object sender, ProcessEventArgs args)
        {
            //  Are we showing diagnostics?
            if (ShowDiagnostics)
            {
                WriteOutput($"{Environment.NewLine}{ProcessInterface.ProcessFileName} exited.", _debugColor);
            }

            if (!IsHandleCreated)
                return;

            //  Read only again.
            Invoke(() => InternalRichTextBox.ReadOnly = true);
        }

        /// <summary>
        /// Handles the KeyDown event of the richTextBoxConsole control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.KeyEventArgs"/> instance containing the event data.</param>
        private void richTextBoxConsole_KeyDown(object? sender, KeyEventArgs e)
        {
            //  Check whether we are in the read-only zone.
            bool isInReadOnlyZone = InternalRichTextBox.SelectionStart < _inputStart;

            //  Are we sending keyboard commands to the process?
            if (SendKeyboardCommandsToProcess && IsProcessRunning)
            {
                //  Get key mappings for this key event?
                IEnumerable<KeyMapping> mappings = KeyMappings.Where(k => k.IsKeyMatch(e));

                //  Go through each mapping, send the message.
                /*foreach (var mapping in mappings)
                {
                    SendKeysEx.SendKeys(CurrentProcessHwnd, mapping.SendKeysMapping);
                    inputWriter.WriteLine(mapping.StreamMapping);
                    WriteInput("\x3", Color.White, false);
                }*/

                //  If we handled a mapping, we're done here.
                if (mappings.Any())
                {
                    e.SuppressKeyPress = true;
                    return;
                }
            }

            //  If we're at the input point and it's backspace, bail.
            if (InternalRichTextBox.SelectionStart <= _inputStart && e.KeyCode is Keys.Back) e.SuppressKeyPress = true;

            //  Are we in the read-only zone?
            if (isInReadOnlyZone)
            {
                //  Allow arrows and Ctrl-C.
                if (!(e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down ||
                      e is { KeyCode: Keys.C, Control: true }))
                {
                    e.SuppressKeyPress = true;
                }
            }

            //  Write the input if we hit return and we're NOT in the read only zone.
            if (e.KeyCode is Keys.Return && !isInReadOnlyZone)
            {
                //  Get the input.
                string input =
                    InternalRichTextBox.Text.Substring(_inputStart, InternalRichTextBox.SelectionStart - _inputStart);

                //  Write the input (without echoing).
                WriteInput(input, _primaryColor, false);
            }
        }

        /// <summary>
        /// Writes the output to the console control.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <param name="color">The color.</param>
        public void WriteOutput(string? output, Color color)
        {
            if (_lastInput.HasValue() && (output == _lastInput || output?.Remove(FORMS_NEWLINE) == _lastInput))
                return;

            if (!IsHandleCreated)
                return;

            Invoke(Write);

            return;

            void Write()
            {
                //  Write the output.
                InternalRichTextBox.SelectionColor = color;
                InternalRichTextBox.SelectedText += output;
                _inputStart = InternalRichTextBox.SelectionStart;
            }
        }

        /// <summary>
        /// Clears the output.
        /// </summary>
        public void ClearOutput()
        {
            InternalRichTextBox.Clear();
            _inputStart = 0;
        }

        /// <summary>
        /// Writes the input to the console control.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="color">The color.</param>
        /// <param name="echo">if set to <c>true</c> echo the input.</param>
        public void WriteInput(string input, Color color, bool echo)
        {
            Invoke(Write);

            return;

            void Write()
            {
                //  Are we echoing?
                if (echo)
                {
                    InternalRichTextBox.SelectionColor = color;
                    InternalRichTextBox.SelectedText += input;
                    _inputStart = InternalRichTextBox.SelectionStart;
                }

                _lastInput = input;

                //  Write the input.
                ProcessInterface.WriteInput(input);

                //  Fire the event.
                FireConsoleInputEvent(input);
            }
        }

        /// <summary>
        /// Runs a process.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="arguments">The arguments.</param>
        public void StartProcess(string fileName, string arguments)
        {
            StartProcess(new ProcessStartInfo(fileName, arguments));
        }

        /// <summary>
        /// Runs a process.
        /// </summary>
        /// <param name="processStartInfo"><see cref="ProcessStartInfo"/> to pass to the process.</param>
        public void StartProcess(ProcessStartInfo processStartInfo)
        {
            //  Are we showing diagnostics?
            if (ShowDiagnostics)
            {
                StringBuilder builder = new();
                builder.Append($"Preparing to run {processStartInfo.FileName}");
                if (processStartInfo.Arguments.HasValue())
                    builder.Append($" with arguments {processStartInfo.Arguments}");
                builder.AppendLine(".");
                WriteOutput(builder.ToString(), _debugColor);
            }

            //  Start the process.
            ProcessInterface.StartProcess(processStartInfo);

            //  If we enable input, make the control not read only.
            if (IsInputEnabled)
                InternalRichTextBox.ReadOnly = false;
        }

        /// <summary>
        /// Stops the process.
        /// </summary>
        public void StopProcess()
        {
            //  Stop the interface.
            ProcessInterface.StopProcess();
        }

        /// <summary>
        /// Fires the console output event.
        /// </summary>
        /// <param name="content">The content.</param>
        private void FireConsoleOutputEvent(string? content)
        {
            //  Get the event.
            ConsoleEventHandler? theEvent = OnConsoleOutput;
            theEvent?.Invoke(this, new ConsoleEventArgs(content));
        }

        /// <summary>
        /// Fires the console input event.
        /// </summary>
        /// <param name="content">The content.</param>
        private void FireConsoleInputEvent(string content)
        {
            //  Get the event.
            ConsoleEventHandler? theEvent = OnConsoleInput;
            theEvent?.Invoke(this, new ConsoleEventArgs(content));
        }

        private readonly Color _primaryColor = Color.White;
        
        private readonly Color _debugColor = Color.Lime;
        
        private readonly Color _errorColor = Color.Red;

        /// <summary>
        /// Current position that input starts at.
        /// </summary>
        private int _inputStart = -1;

        /// <summary>
        /// The is input enabled flag.
        /// </summary>
        private bool _isInputEnabled = true;

        /// <summary>
        /// The last input string (used so that we can make sure we don't echo input twice).
        /// </summary>
        private string? _lastInput;

        /// <summary>
        /// Occurs when console output is produced.
        /// </summary>
        public event ConsoleEventHandler? OnConsoleOutput;

        /// <summary>
        /// Occurs when console input is produced.
        /// </summary>
        public event ConsoleEventHandler? OnConsoleInput;

        /// <summary>
        /// Gets or sets a value indicating whether to show diagnostics.
        /// </summary>
        /// <value>
        ///   <c>true</c> if show diagnostics; otherwise, <c>false</c>.
        /// </value>
        [Category("Console Control"), Description("Show diagnostic information, such as exceptions.")]
        public bool ShowDiagnostics { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is input enabled.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is input enabled; otherwise, <c>false</c>.
        /// </value>
        [Category("Console Control"), Description("If true, the user can key in input.")]
        public bool IsInputEnabled
        {
            get => _isInputEnabled;
            set
            {
                _isInputEnabled = value;
                if (IsProcessRunning)
                    InternalRichTextBox.ReadOnly = !value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [send keyboard commands to process].
        /// </summary>
        /// <value>
        /// 	<c>true</c> if [send keyboard commands to process]; otherwise, <c>false</c>.
        /// </value>
        [Category("Console Control"),
         Description("If true, special keyboard commands like Ctrl-C and tab are sent to the process.")]
        public bool SendKeyboardCommandsToProcess { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance is process running.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is process running; otherwise, <c>false</c>.
        /// </value>
        [Browsable(false)]
        public bool IsProcessRunning => ProcessInterface.IsProcessRunning;

        /// <summary>
        /// Gets the internal rich text box.
        /// </summary>
        [Browsable(false)]
        public RichTextBox InternalRichTextBox { get; private set; }

        /// <summary>
        /// Gets the process interface.
        /// </summary>
        [Browsable(false)]
        public ProcessInterface ProcessInterface { get; } = new();

        /// <summary>
        /// Gets the key mappings.
        /// </summary>
        [Browsable(false)]
        public IEnumerable<KeyMapping> KeyMappings { get; } = new List<KeyMapping>
        {
            //  Map 'tab'.
            new(false, false, false, Keys.Tab, "{TAB}", "\t"),

            //  Map 'Ctrl-C'.
            new(true, false, false, Keys.C, "^(c)", "\x03" + FORMS_NEWLINE)
        };

        /// <summary>
        /// Gets or sets the font of the text displayed by the control.
        /// </summary>
        /// <returns>The <see cref="T:System.Drawing.Font" /> to apply to the text displayed by the control. The default is the value of the <see cref="P:System.Windows.Forms.Control.DefaultFont" /> property.</returns>
        ///   <PermissionSet>
        ///   <IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true" />
        ///   <IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true" />
        ///   <IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="UnmanagedCode, ControlEvidence" />
        ///   <IPermission class="System.Diagnostics.PerformanceCounterPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true" />
        ///   </PermissionSet>
        public override Font Font
        {
            //  Return the base class font.
            get => base.Font;
            set
            {
                //  Set the base class font...
                base.Font = value;

                //  ...and the internal control font.
                InternalRichTextBox.Font = value;
            }
        }

        /// <summary>
        /// Gets or sets the background color for the control.
        /// </summary>
        /// <returns>A <see cref="T:System.Drawing.Color" /> that represents the background color of the control. The default is the value of the <see cref="P:System.Windows.Forms.Control.DefaultBackColor" /> property.</returns>
        ///   <PermissionSet>
        ///   <IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true" />
        ///   </PermissionSet>
        public override Color BackColor
        {
            //  Return the base class background.
            get => base.BackColor;
            set
            {
                //  Set the base class background...
                base.BackColor = value;

                //  ...and the internal control background.
                InternalRichTextBox.BackColor = value;
            }
        }
    }

    /// <summary>
    /// Used to allow us to find resources properly.
    /// </summary>
    public class Resfinder { }
}