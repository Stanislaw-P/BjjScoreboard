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
        private string _weightInput = "77";
        private string _currentDiscipline = "BJJ";

        public FighterViewModel RedFighter { get; }
        public FighterViewModel BlueFighter { get; }

        public List<string> Genders { get; } = new List<string> { "Мужчины", "Женщины" };
        public List<string> Teams { get; } = new List<string> { "", "Berserker's Team", "Gracie Barra", "Alliance", "Checkmat", "Atos Jiu-Jitsu" };

        public string SelectedGender { get => _selectedGender; set { _selectedGender = value; OnPropertyChanged(); } }
        public string WeightInput { get => _weightInput; set { _weightInput = value; OnPropertyChanged(); } }

        public string CurrentDiscipline
        {
            get => _currentDiscipline;
            set { _currentDiscipline = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsBjjActive)); OnPropertyChanged(nameof(IsFightingActive)); ResetMatch(); }
        }

        public bool IsBjjActive => CurrentDiscipline == "BJJ";
        public bool IsFightingActive => CurrentDiscipline == "FIGHTING";

        public int MatchDurationMinutes
        {
            get => _matchDurationMinutes;
            set { if (value > 0 && value <= 60) { _matchDurationMinutes = value; OnPropertyChanged(); if (!IsRunning) ResetMatchTime(); } }
        }

        public string TimeDisplay => _timeRemaining.ToString(@"mm\:ss");
        public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimerColor)); } }
        public string TimerColor => IsRunning ? "#FFFFFF" : "#FFCC00";

        public ICommand ModifyMatchDurationCmd { get; }
        public ICommand TogglePauseCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand SwitchDisciplineCmd { get; }

        public MainViewModel()
        {
            RedFighter = new FighterViewModel(true, ThisFighterPenalized, ExecuteSubLogic, ExecuteDqLogic, () => CheckFullIppon());
            BlueFighter = new FighterViewModel(false, ThisFighterPenalized, ExecuteSubLogic, ExecuteDqLogic, () => CheckFullIppon());

            ResetMatchTime();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;

            ModifyMatchDurationCmd = new RelayCommand(p => MatchDurationMinutes += Convert.ToInt32(p));
            TogglePauseCommand = new RelayCommand(_ => ToggleTimer());
            ResetCommand = new RelayCommand(_ => ResetMatch());
            SwitchDisciplineCmd = new RelayCommand(p => CurrentDiscipline = p.ToString());
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_timeRemaining > TimeSpan.Zero) { _timeRemaining = _timeRemaining.Subtract(TimeSpan.FromSeconds(1)); OnPropertyChanged(nameof(TimeDisplay)); }
            else { _timer.Stop(); IsRunning = false; DetermineWinnerByPoints(); }
        }

        private void ToggleTimer() { if (IsRunning) { _timer.Stop(); IsRunning = false; } else { _timer.Start(); IsRunning = true; } }
        private void ResetMatchTime() { _timeRemaining = TimeSpan.FromMinutes(MatchDurationMinutes); OnPropertyChanged(nameof(TimeDisplay)); }

        private void ResetMatch()
        {
            _timer.Stop(); IsRunning = false; ResetMatchTime();
            RedFighter.Reset(); BlueFighter.Reset();
        }

        private void CheckFullIppon()
        {
            if (CurrentDiscipline != "FIGHTING") return;

            // Условие Full Ippon: есть Иппон в Part 1 И Part 2 И (любой из вариантов Part 3)
            if (RedFighter.Part1Ippon && RedFighter.Part2Ippon && (RedFighter.Part3Ippon2Points || RedFighter.Part3Ippon3Points))
                ExecuteFullIpponWin(RedFighter);
            else if (BlueFighter.Part1Ippon && BlueFighter.Part2Ippon && (BlueFighter.Part3Ippon2Points || BlueFighter.Part3Ippon3Points))
                ExecuteFullIpponWin(BlueFighter);
        }

        private void ExecuteFullIpponWin(FighterViewModel winner)
        {
            _timer.Stop(); IsRunning = false;
            FighterViewModel loser = (winner == RedFighter) ? BlueFighter : RedFighter;
            winner.Points = 50; loser.Points = 0;
            ApplyColors(winner);
        }

        private void ThisFighterPenalized(bool isRed)
        {
            FighterViewModel offender = isRed ? RedFighter : BlueFighter;
            FighterViewModel opponent = isRed ? BlueFighter : RedFighter;

            if (CurrentDiscipline == "BJJ")
            {
                switch (offender.Penalties)
                {
                    case 2: opponent.Advantages += 1; break;
                    case 3: opponent.Points += 2; break;
                    case 4: ExecuteDqLogic(offender); break;
                }
            }
            else // Регламент JJIF Fighting System (Наказания добавляют очки оппоненту)
            {
                switch (offender.Penalties)
                {
                    case 1: opponent.Points += 1; break;
                    case 2: opponent.Points += 2; break;
                    case 3: opponent.Points += 3; break;
                    case 4: ExecuteDqLogic(offender); break;
                }
            }
        }

        private void ExecuteSubLogic(FighterViewModel submissionWinner)
        {
            _timer.Stop(); IsRunning = false;
            FighterViewModel loser = (submissionWinner == RedFighter) ? BlueFighter : RedFighter;
            submissionWinner.Points = 50; loser.Points = 0;
            ApplyColors(submissionWinner);
        }

        private void ExecuteDqLogic(FighterViewModel penalizedFighter)
        {
            _timer.Stop(); IsRunning = false;
            FighterViewModel winner = (penalizedFighter == RedFighter) ? BlueFighter : RedFighter;
            penalizedFighter.Points = 0; winner.Points = 50;
            ApplyColors(winner);
        }

        private void ApplyColors(FighterViewModel winner)
        {
            if (winner == RedFighter) { RedFighter.RowBackground = "#24A148"; BlueFighter.RowBackground = "#393939"; }
            else { BlueFighter.RowBackground = "#24A148"; RedFighter.RowBackground = "#393939"; }
        }

        private void DetermineWinnerByPoints()
        {
            if (RedFighter.Points > BlueFighter.Points) ApplyColors(RedFighter);
            else if (BlueFighter.Points > RedFighter.Points) ApplyColors(BlueFighter);
            else if (RedFighter.Advantages > BlueFighter.Advantages) ApplyColors(RedFighter);
            else if (BlueFighter.Advantages > RedFighter.Advantages) ApplyColors(BlueFighter);
            else if (RedFighter.Penalties < BlueFighter.Penalties) ApplyColors(RedFighter);
            else if (BlueFighter.Penalties < RedFighter.Penalties) ApplyColors(BlueFighter);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class FighterViewModel : INotifyPropertyChanged
    {
        private string _name = "";
        private string _team = "";
        private int _points;
        private int _advantages;
        private int _penalties;
        private string _rowBackground = "#172637";
        private bool _p1, _p2, _p3_2, _p3_3;

        private readonly Action<bool> _onPenaltyChanged;
        private readonly Action<FighterViewModel> _onSubTriggered;
        private readonly Action<FighterViewModel> _onDqTriggered;
        private readonly Action _onStateChanged;
        private readonly bool _isRed;

        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Team { get => _team; set { _team = value; OnPropertyChanged(); } }
        public int Points { get => _points; set { _points = Math.Max(0, value); OnPropertyChanged(); } }
        public int Advantages { get => _advantages; set { _advantages = Math.Max(0, value); OnPropertyChanged(); } }
        public int Penalties { get => _penalties; set { _penalties = Math.Max(0, value); OnPropertyChanged(); } }
        public string RowBackground { get => _rowBackground; set { _rowBackground = value; OnPropertyChanged(); } }

        // Связываем переключатели Иппонов с автоматическим изменением очков на табло (+2 или +3)
        public bool Part1Ippon
        {
            get => _p1;
            set { if (_p1 != value) { _p1 = value; Points += value ? 2 : -2; OnPropertyChanged(); _onStateChanged(); } }
        }
        public bool Part2Ippon
        {
            get => _p2;
            set { if (_p2 != value) { _p2 = value; Points += value ? 2 : -2; OnPropertyChanged(); _onStateChanged(); } }
        }
        public bool Part3Ippon2Points
        {
            get => _p3_2;
            set { if (_p3_2 != value) { _p3_2 = value; Points += value ? 2 : -2; OnPropertyChanged(); _onStateChanged(); } }
        }
        public bool Part3Ippon3Points
        {
            get => _p3_3;
            set { if (_p3_3 != value) { _p3_3 = value; Points += value ? 3 : -3; OnPropertyChanged(); _onStateChanged(); } }
        }

        public ICommand ModifyPointsCmd { get; }
        public ICommand ModifyAdvantagesCmd { get; }
        public ICommand ModifyPenaltiesCmd { get; }
        public ICommand WinBySubCmd { get; }
        public ICommand WinByDqCmd { get; }

        public FighterViewModel(bool isRed, Action<bool> onPenaltyChanged, Action<FighterViewModel> onSubTriggered, Action<FighterViewModel> onDqTriggered, Action onStateChanged)
        {
            _isRed = isRed; _onPenaltyChanged = onPenaltyChanged; _onSubTriggered = onSubTriggered; _onDqTriggered = onDqTriggered; _onStateChanged = onStateChanged;

            ModifyPointsCmd = new RelayCommand(p => Points += Convert.ToInt32(p));
            ModifyAdvantagesCmd = new RelayCommand(p => Advantages += Convert.ToInt32(p));
            ModifyPenaltiesCmd = new RelayCommand(p => {
                int diff = Convert.ToInt32(p);
                if (diff > 0 && Penalties < 4) { Penalties++; _onPenaltyChanged(_isRed); }
                else if (diff < 0) { Penalties--; }
            });
            WinBySubCmd = new RelayCommand(_ => _onSubTriggered(this));
            WinByDqCmd = new RelayCommand(_ => _onDqTriggered(this));
        }

        public void Reset()
        {
            Name = ""; Team = ""; _points = 0; Advantages = 0; Penalties = 0;
            _p1 = false; _p2 = false; _p3_2 = false; _p3_3 = false;
            RowBackground = "#172637";
            OnPropertyChanged(nameof(Points)); OnPropertyChanged(nameof(Part1Ippon));
            OnPropertyChanged(nameof(Part2Ippon)); OnPropertyChanged(nameof(Part3Ippon2Points)); OnPropertyChanged(nameof(Part3Ippon3Points));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}