using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;

namespace BjjScoreboard
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private enum MatchPhase { Regulation, RegulationEnded, GoldenScore, Finished, GoldenScoreFinished }

        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _clock = new();
        private MatchPhase _phase;
        private TimeSpan _regulationDuration;
        private Action? _undoGoldenScoreResult;
        private FighterViewModel? _winner;
        private int _matchDurationMinutes = 5;
        private bool _isRunning;
        private string _selectedGender = "Мужчины";
        private string _weightInput = "-77";

        public FighterViewModel RedFighter { get; }
        public FighterViewModel BlueFighter { get; }

        public List<string> Genders { get; } = new List<string> { "Мужчины", "Женщины" };
        public List<string> Teams { get; private set; }

        public string SelectedGender { get => _selectedGender; set { _selectedGender = value; OnPropertyChanged(); } }
        public string WeightInput { get => _weightInput; set { _weightInput = value; OnPropertyChanged(); } }

        public int MatchDurationMinutes
        {
            get => _matchDurationMinutes;
            set
            {
                if (_phase == MatchPhase.Regulation && value > 0 && value <= 60)
                {
                    _matchDurationMinutes = value;
                    OnPropertyChanged();
                    if (!IsRunning) ResetMatchTime();
                }
            }
        }

        public string TimeDisplay
        {
            get
            {
                var seconds = (long)(_phase is MatchPhase.GoldenScore or MatchPhase.GoldenScoreFinished
                    ? Math.Floor(_clock.Elapsed.TotalSeconds)
                    : Math.Max(0, Math.Ceiling((_regulationDuration - _clock.Elapsed).TotalSeconds)));
                return $"{seconds / 60:00}:{seconds % 60:00}";
            }
        }

        public bool CanStartGoldenScore => _phase == MatchPhase.RegulationEnded && GetRegulationWinner() is null;
        public bool ShowGoldenScore => CanStartGoldenScore || _phase is MatchPhase.GoldenScore or MatchPhase.GoldenScoreFinished;
        public bool CanUndoGoldenScore => _undoGoldenScoreResult is not null;
        public bool ShowDurationControls => _phase == MatchPhase.Regulation;
        public bool HasWinner => _winner is not null;
        public string WinnerText => _winner is null ? "" : _winner == RedFighter ? "WINNER RED" : "WINNER BLUE";
        public string WinnerColor => _winner == RedFighter ? "#DC143C" : "#0F62FE";
        private bool CanEditScore => _phase is not (MatchPhase.Finished or MatchPhase.GoldenScoreFinished);

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                _isRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimerColor));
            }
        }

        public string TimerColor => IsRunning ? "#FFFFFF" : "#FFCC00";

        public ICommand ModifyMatchDurationCmd { get; }
        public ICommand StartCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand TogglePauseCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand StartGoldenScoreCommand { get; }
        public ICommand UndoGoldenScoreCommand { get; }
        public event EventHandler? StopSignalRequested;

        public MainViewModel()
        {
            // Сначала загружаем команды из файла или дефолтные
            LoadTeams();

            RedFighter = CreateFighter();
            BlueFighter = CreateFighter();

            ResetMatchTime();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += (_, _) => UpdateClock();

            ModifyMatchDurationCmd = new RelayCommand(p => MatchDurationMinutes += Convert.ToInt32(p));
            StartCommand = new RelayCommand(_ => StartTimer());
            PauseCommand = new RelayCommand(_ => PauseTimer());
            TogglePauseCommand = new RelayCommand(_ => ToggleTimer());
            ResetCommand = new RelayCommand(_ => ResetMatch());
            StartGoldenScoreCommand = new RelayCommand(_ => StartGoldenScore(), () => CanStartGoldenScore);
            UndoGoldenScoreCommand = new RelayCommand(_ => UndoGoldenScoreResult(), () => CanUndoGoldenScore);
        }

        private FighterViewModel CreateFighter() =>
            new(RecordScoreChange, () => CanEditScore, ChangePenalty, FinishByStoppage, ExecuteDqLogic);

        private void LoadTeams()
        {
            // Список по умолчанию на случай отсутствия или поломки файла
            var defaultTeams = new List<string>
            {
                "Os Bagatar BJJ", "Динамо", "ФАТ", "Триумф", "РСО-Алания",
                "Кабардино-Балкарская Республика", "Чеченская Республика", "Республика Дагестан"
            };

            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "teams.json");

            if (File.Exists(filePath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(filePath);
                    var parsedTeams = JsonSerializer.Deserialize<List<string>>(jsonContent);

                    if (parsedTeams != null && parsedTeams.Count > 0)
                    {
                        Teams = parsedTeams;
                    }
                    else
                    {
                        Teams = defaultTeams;
                    }
                }
                catch
                {
                    // В случае любой ошибки парсинга откатываемся на дефолтный список
                    Teams = defaultTeams;
                }
            }
            else
            {
                Teams = defaultTeams;
            }

            // Автоматически добавляем пустую строку в самое начало списка
            Teams.Insert(0, "");
        }

        private void UpdateClock()
        {
            if (_phase == MatchPhase.Regulation && _clock.Elapsed >= _regulationDuration)
            {
                PauseTimer();
                _phase = MatchPhase.RegulationEnded;
                RefreshRegulationResult();
                StopSignalRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                OnPropertyChanged(nameof(TimeDisplay));
            }
        }

        private void StartTimer()
        {
            if (CanStartGoldenScore)
            {
                StartGoldenScore();
                return;
            }

            if (_phase is not (MatchPhase.Regulation or MatchPhase.GoldenScore) || IsRunning) return;
            _clock.Start();
            _timer.Start();
            IsRunning = true;
        }

        private void PauseTimer()
        {
            _clock.Stop();
            _timer.Stop();
            IsRunning = false;
            OnPropertyChanged(nameof(TimeDisplay));
        }

        private void ToggleTimer()
        {
            if (IsRunning)
            {
                PauseTimer();
                UpdateClock();
            }
            else StartTimer();
        }

        private void StartGoldenScore()
        {
            if (!CanStartGoldenScore) return;
            _phase = MatchPhase.GoldenScore;
            _clock.Reset();
            StartTimer();
            NotifyMatchState();
        }

        private void ResetMatchTime()
        {
            _clock.Reset();
            _regulationDuration = TimeSpan.FromMinutes(MatchDurationMinutes);
            OnPropertyChanged(nameof(TimeDisplay));
        }

        private void ResetMatch()
        {
            PauseTimer();
            _phase = MatchPhase.Regulation;
            _undoGoldenScoreResult = null;
            ResetMatchTime();
            RedFighter.Reset();
            BlueFighter.Reset();
            ApplyResult(null);
            NotifyMatchState();
        }

        private void RecordScoreChange(Action change)
        {
            if (!CanEditScore) return;

            if (_phase == MatchPhase.GoldenScore)
            {
                var redScore = FighterScore.Capture(RedFighter);
                var blueScore = FighterScore.Capture(BlueFighter);
                change();

                if (_phase == MatchPhase.GoldenScore)
                {
                    if (redScore.HasIncreased(RedFighter)) DeclareWinner(RedFighter);
                    else if (blueScore.HasIncreased(BlueFighter)) DeclareWinner(BlueFighter);
                }

                if (_phase == MatchPhase.GoldenScoreFinished)
                {
                    _undoGoldenScoreResult = () =>
                    {
                        redScore.Restore(RedFighter);
                        blueScore.Restore(BlueFighter);
                    };
                    NotifyMatchState();
                }
                return;
            }

            change();
            if (_phase == MatchPhase.RegulationEnded) RefreshRegulationResult();
            else if (_phase == MatchPhase.Finished) NotifyMatchState();
        }

        private void UndoGoldenScoreResult()
        {
            if (_undoGoldenScoreResult is null) return;
            _undoGoldenScoreResult();
            _undoGoldenScoreResult = null;
            _phase = MatchPhase.GoldenScore;
            ApplyResult(null);
            NotifyMatchState();
        }

        private void ChangePenalty(FighterViewModel offender, int change)
        {
            var previous = offender.Penalties;
            var current = Math.Clamp(previous + change, 0, 4);
            if (previous == current) return;

            var opponent = OpponentOf(offender);
            offender.Penalties = current;
            if (previous < 2 && current >= 2) opponent.Advantages++;
            if (previous < 3 && current >= 3) opponent.Points += 2;
            if (previous >= 2 && current < 2) opponent.Advantages--;
            if (previous >= 3 && current < 3) opponent.Points -= 2;
            if (current == 4) ExecuteDqLogic(offender);
        }

        private void FinishByStoppage(FighterViewModel winner)
        {
            winner.Points = 50;
            OpponentOf(winner).Points = 0;
            DeclareWinner(winner);
        }

        private FighterViewModel OpponentOf(FighterViewModel fighter) => fighter == RedFighter ? BlueFighter : RedFighter;

        private void ExecuteDqLogic(FighterViewModel penalizedFighter) => FinishByStoppage(OpponentOf(penalizedFighter));

        private void DeclareWinner(FighterViewModel winner)
        {
            PauseTimer();
            _phase = _phase == MatchPhase.GoldenScore ? MatchPhase.GoldenScoreFinished : MatchPhase.Finished;
            ApplyResult(winner);
            StopSignalRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyResult(FighterViewModel? winner)
        {
            if (_winner == winner) return;
            _winner = winner;
            RedFighter.RowBackground = ResultColor(RedFighter);
            BlueFighter.RowBackground = ResultColor(BlueFighter);
            OnPropertyChanged(nameof(WinnerText));
            OnPropertyChanged(nameof(WinnerColor));
            OnPropertyChanged(nameof(HasWinner));

            string ResultColor(FighterViewModel fighter) =>
                winner is null ? FighterViewModel.DefaultRowBackground : winner == fighter ? "#24A148" : "#393939";
        }

        private FighterViewModel? GetRegulationWinner()
        {
            if (RedFighter.Points != BlueFighter.Points)
                return RedFighter.Points > BlueFighter.Points ? RedFighter : BlueFighter;
            if (RedFighter.Advantages != BlueFighter.Advantages)
                return RedFighter.Advantages > BlueFighter.Advantages ? RedFighter : BlueFighter;
            return null;
        }

        private void RefreshRegulationResult()
        {
            ApplyResult(GetRegulationWinner());
            NotifyMatchState();
        }

        private void NotifyMatchState()
        {
            OnPropertyChanged(nameof(TimeDisplay));
            OnPropertyChanged(nameof(CanStartGoldenScore));
            OnPropertyChanged(nameof(ShowGoldenScore));
            OnPropertyChanged(nameof(CanUndoGoldenScore));
            OnPropertyChanged(nameof(ShowDurationControls));
            CommandManager.InvalidateRequerySuggested();
        }

        private readonly record struct FighterScore(int Points, int Advantages, int Penalties)
        {
            public static FighterScore Capture(FighterViewModel fighter) =>
                new(fighter.Points, fighter.Advantages, fighter.Penalties);

            public bool HasIncreased(FighterViewModel fighter) => fighter.Points > Points || fighter.Advantages > Advantages;

            public void Restore(FighterViewModel fighter)
            {
                fighter.Points = Points;
                fighter.Advantages = Advantages;
                fighter.Penalties = Penalties;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class FighterViewModel : INotifyPropertyChanged
    {
        private string _name = "";
        private string _team = "";
        private int _points;
        private int _advantages;
        private int _penalties;
        internal const string DefaultRowBackground = "#172637";
        private string _rowBackground = DefaultRowBackground;

        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Team { get => _team; set { _team = value ?? ""; OnPropertyChanged(); } }
        public int Points { get => _points; set { _points = Math.Max(0, value); OnPropertyChanged(); } }
        public int Advantages { get => _advantages; set { _advantages = Math.Max(0, value); OnPropertyChanged(); } }
        public int Penalties { get => _penalties; set { _penalties = Math.Max(0, value); OnPropertyChanged(); } }
        public string RowBackground { get => _rowBackground; set { _rowBackground = value; OnPropertyChanged(); } }

        public ICommand ModifyPointsCmd { get; }
        public ICommand ModifyAdvantagesCmd { get; }
        public ICommand ModifyPenaltiesCmd { get; }
        public ICommand WinBySubCmd { get; }
        public ICommand WinByDqCmd { get; }

        public FighterViewModel(
            Action<Action> recordScoreChange,
            Func<bool> canEditScore,
            Action<FighterViewModel, int> changePenalty,
            Action<FighterViewModel> onSubmission,
            Action<FighterViewModel> onDisqualification)
        {
            ModifyPointsCmd = ScoreCommand(p => Points += Convert.ToInt32(p));
            ModifyAdvantagesCmd = ScoreCommand(p => Advantages += Convert.ToInt32(p));
            ModifyPenaltiesCmd = ScoreCommand(p => changePenalty(this, Convert.ToInt32(p)));
            WinBySubCmd = ScoreCommand(_ => onSubmission(this));
            WinByDqCmd = ScoreCommand(_ => onDisqualification(this));

            ICommand ScoreCommand(Action<object?> change) =>
                new RelayCommand(p => recordScoreChange(() => change(p)), canEditScore);
        }

        public void Reset()
        {
            Name = "";
            Team = "";
            Points = 0;
            Advantages = 0;
            Penalties = 0;
            RowBackground = DefaultRowBackground;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter)
        {
            if (CanExecute(parameter)) _execute(parameter);
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
