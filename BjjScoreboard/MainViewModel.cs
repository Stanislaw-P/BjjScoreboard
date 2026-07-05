using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace BjjScoreboard
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private DispatcherTimer _timer;
        private TimeSpan _timeRemaining;
        private int _matchDurationMinutes = 5;
        private bool _isRunning;
        private string _selectedGender = "Мужчины";
        private string _weightInput = "-77";

        public FighterViewModel RedFighter { get; }
        public FighterViewModel BlueFighter { get; }

        public List<string> Genders { get; } = new List<string> { "Мужчины", "Женщины" };
        public List<string> Teams { get; } = new List<string> { "", "Os Bagatar BJJ", "Динамо", "ФАТ", "Триумф", "РСО-Алания",
            "Кабардино-Балкарская Республика", "Чеченская Республика", "Республика Дагестан"};

        public string SelectedGender { get => _selectedGender; set { _selectedGender = value; OnPropertyChanged(); } }
        public string WeightInput { get => _weightInput; set { _weightInput = value; OnPropertyChanged(); } }

        public int MatchDurationMinutes
        {
            get => _matchDurationMinutes;
            set
            {
                if (value > 0 && value <= 60)
                {
                    _matchDurationMinutes = value;
                    OnPropertyChanged();
                    if (!IsRunning) ResetMatchTime();
                }
            }
        }

        public string TimeDisplay => _timeRemaining.ToString(@"mm\:ss");

        public bool IsRunning
        {
            get => _isRunning;
            set
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

        public MainViewModel()
        {
            RedFighter = new FighterViewModel(true, ThisFighterPenalized, ExecuteSubLogic, ExecuteDqLogic);
            BlueFighter = new FighterViewModel(false, ThisFighterPenalized, ExecuteSubLogic, ExecuteDqLogic);

            ResetMatchTime();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            ModifyMatchDurationCmd = new RelayCommand(p => MatchDurationMinutes += Convert.ToInt32(p));
            StartCommand = new RelayCommand(_ => StartTimer());
            PauseCommand = new RelayCommand(_ => PauseTimer());
            TogglePauseCommand = new RelayCommand(_ => ToggleTimer());
            ResetCommand = new RelayCommand(_ => ResetMatch());
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_timeRemaining > TimeSpan.Zero)
            {
                _timeRemaining = _timeRemaining.Subtract(TimeSpan.FromSeconds(1));
                OnPropertyChanged(nameof(TimeDisplay));
            }
            else
            {
                _timer.Stop();
                IsRunning = false;
                DetermineWinnerByPoints();
            }
        }

        private void StartTimer() { _timer.Start(); IsRunning = true; }
        private void PauseTimer() { _timer.Stop(); IsRunning = false; }
        private void ToggleTimer() { if (IsRunning) PauseTimer(); else StartTimer(); }

        private void ResetMatchTime()
        {
            _timeRemaining = TimeSpan.FromMinutes(MatchDurationMinutes);
            OnPropertyChanged(nameof(TimeDisplay));
        }

        private void ResetMatch()
        {
            _timer.Stop();
            IsRunning = false;
            ResetMatchTime();
            RedFighter.Reset();
            BlueFighter.Reset();
        }

        private void ThisFighterPenalized(bool isRed)
        {
            FighterViewModel offender = isRed ? RedFighter : BlueFighter;
            FighterViewModel opponent = isRed ? BlueFighter : RedFighter;

            switch (offender.Penalties)
            {
                case 2: opponent.Advantages += 1; break;
                case 3: opponent.Points += 2; break;
                case 4: ExecuteDqLogic(offender); break;
            }
        }

        private void ExecuteSubLogic(FighterViewModel submissionWinner)
        {
            _timer.Stop();
            IsRunning = false;
            FighterViewModel loser = (submissionWinner == RedFighter) ? BlueFighter : RedFighter;
            submissionWinner.Points = 50;
            dynamicLoserPointsSet(loser);
            ApplyColors(submissionWinner);
        }

        private void ExecuteDqLogic(FighterViewModel penalizedFighter)
        {
            _timer.Stop();
            IsRunning = false;
            FighterViewModel winner = (penalizedFighter == RedFighter) ? BlueFighter : RedFighter;
            penalizedFighter.Points = 0;
            winner.Points = 50;
            ApplyColors(winner);
        }

        private void dynamicLoserPointsSet(FighterViewModel loser)
        {
            loser.Points = 0;
        }

        private void DeclareWinner(FighterViewModel winner)
        {
            _timer.Stop();
            IsRunning = false;
            ApplyColors(winner);
        }

        private void ApplyColors(FighterViewModel winner)
        {
            if (winner == RedFighter)
            {
                RedFighter.RowBackground = "#24A148";
                BlueFighter.RowBackground = "#393939";
            }
            else
            {
                BlueFighter.RowBackground = "#24A148";
                RedFighter.RowBackground = "#393939";
            }
        }

        private void DetermineWinnerByPoints()
        {
            if (RedFighter.Points > BlueFighter.Points) DeclareWinner(RedFighter);
            else if (BlueFighter.Points > RedFighter.Points) DeclareWinner(BlueFighter);
            else if (RedFighter.Advantages > BlueFighter.Advantages) DeclareWinner(RedFighter);
            else if (BlueFighter.Advantages > RedFighter.Advantages) DeclareWinner(BlueFighter);
            else if (RedFighter.Penalties < BlueFighter.Penalties) DeclareWinner(RedFighter);
            else if (BlueFighter.Penalties < RedFighter.Penalties) DeclareWinner(BlueFighter);
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
        private string _rowBackground = "#172637";

        private readonly Action<bool> _onPenaltyChanged;
        private readonly Action<FighterViewModel> _onSubTriggered;
        private readonly Action<FighterViewModel> _onDqTriggered;
        private readonly bool _isRed;

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

        public FighterViewModel(bool isRed, Action<bool> onPenaltyChanged, Action<FighterViewModel> onSubTriggered, Action<FighterViewModel> onDqTriggered)
        {
            _isRed = isRed;
            _onPenaltyChanged = onPenaltyChanged;
            _onSubTriggered = onSubTriggered;
            _onDqTriggered = onDqTriggered;

            ModifyPointsCmd = new RelayCommand(p => Points += Convert.ToInt32(p));
            ModifyAdvantagesCmd = new RelayCommand(p => Advantages += Convert.ToInt32(p));
            ModifyPenaltiesCmd = new RelayCommand(p => {
                int diff = Convert.ToInt32(p);
                if (diff > 0 && Penalties < 4)
                {
                    Penalties++;
                    _onPenaltyChanged(_isRed);
                }
                else if (diff < 0 && Penalties > 0)
                {
                    Penalties--;
                }
            });

            WinBySubCmd = new RelayCommand(_ => _onSubTriggered(this));
            WinByDqCmd = new RelayCommand(_ => _onDqTriggered(this));
        }

        public void Reset()
        {
            Name = "";
            Team = "";
            Points = 0;
            Advantages = 0;
            Penalties = 0;
            RowBackground = "#172637";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        public RelayCommand(Action<object> execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}