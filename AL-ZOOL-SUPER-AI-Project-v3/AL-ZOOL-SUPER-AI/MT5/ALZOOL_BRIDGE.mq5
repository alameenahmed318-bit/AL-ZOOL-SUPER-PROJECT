#property strict
#property version "1.10"
#property description "AL-ZOOL MT5 status and M1 candle data bridge (read-only)"

input int TimerSeconds = 1;
input int CandleCount = 150;

int OnInit()
{
   FolderCreate("ALZOOL", FILE_COMMON);
   EventSetTimer(MathMax(1, TimerSeconds));
   Print("AL-ZOOL data bridge started. Trading commands are not enabled.");
   return INIT_SUCCEEDED;
}

void OnDeinit(const int reason)
{
   EventKillTimer();
}

void WriteStatus()
{
   int h = FileOpen("ALZOOL\\status.csv",
      FILE_WRITE | FILE_CSV | FILE_COMMON | FILE_ANSI | FILE_SHARE_READ,
      ';');

   if(h == INVALID_HANDLE)
   {
      Print("AL-ZOOL status file error: ", GetLastError());
      return;
   }

   FileWrite(h,
      (long)AccountInfoInteger(ACCOUNT_LOGIN),
      AccountInfoString(ACCOUNT_SERVER),
      DoubleToString(AccountInfoDouble(ACCOUNT_BALANCE), 2),
      DoubleToString(AccountInfoDouble(ACCOUNT_EQUITY), 2),
      _Symbol,
      DoubleToString(SymbolInfoDouble(_Symbol, SYMBOL_BID), _Digits),
      DoubleToString(SymbolInfoDouble(_Symbol, SYMBOL_ASK), _Digits),
      PositionsTotal(),
      TimeToString(TimeLocal(), TIME_DATE | TIME_SECONDS));

   FileClose(h);
}

void WriteCandles()
{
   MqlRates rates[];
   ArraySetAsSeries(rates, false);

   int copied = CopyRates(_Symbol, PERIOD_M1, 0, MathMax(60, CandleCount), rates);
   if(copied < 60)
   {
      Print("AL-ZOOL waiting for M1 history. Copied candles: ", copied);
      return;
   }

   int h = FileOpen("ALZOOL\\candles.csv",
      FILE_WRITE | FILE_CSV | FILE_COMMON | FILE_ANSI | FILE_SHARE_READ,
      ';');

   if(h == INVALID_HANDLE)
   {
      Print("AL-ZOOL candles file error: ", GetLastError());
      return;
   }

   for(int i = 0; i < copied; i++)
   {
      FileWrite(h,
         TimeToString(rates[i].time, TIME_DATE | TIME_MINUTES),
         DoubleToString(rates[i].open, _Digits),
         DoubleToString(rates[i].high, _Digits),
         DoubleToString(rates[i].low, _Digits),
         DoubleToString(rates[i].close, _Digits),
         (long)rates[i].tick_volume);
   }

   FileClose(h);
}

void OnTimer()
{
   WriteStatus();
   WriteCandles();
}
