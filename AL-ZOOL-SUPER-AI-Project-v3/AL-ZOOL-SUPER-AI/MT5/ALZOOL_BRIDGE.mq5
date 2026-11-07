#property strict
#property version "1.21"
#property description "AL-ZOOL MT5 bridge: market data and command execution on connected MT5 accounts"

#include <Trade/Trade.mqh>

input int TimerSeconds = 1;
input int CandleCount = 150;
input int MaxDeviationPoints = 30;

CTrade trade;

bool IsGoldSymbol(const string symbol)
{
   string s = symbol;
   StringToUpper(s);
   return (StringFind(s, "XAUUSD") == 0 ||
           (StringFind(s, "XAU") >= 0 && StringFind(s, "USD") >= 0));
}


void WriteStatus()
{
   double floating = 0.0;
   int count = 0;
   for(int i = PositionsTotal() - 1; i >= 0; --i)
   {
      ulong ticket = PositionGetTicket(i);
      if(ticket == 0) continue;
      if(PositionGetString(POSITION_SYMBOL) == _Symbol)
      {
         floating += PositionGetDouble(POSITION_PROFIT) +
                     PositionGetDouble(POSITION_SWAP);
         count++;
      }
   }

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
      count,
      TimeToString(TimeLocal(), TIME_DATE | TIME_SECONDS),
      DoubleToString(floating, 2),
      IsDemoAccount() ? "DEMO" : "LIVE");
   FileClose(h);
}

void WriteCandles()
{
   MqlRates rates[];
   ArraySetAsSeries(rates, false);

   // Request history on every timer tick. MT5 may initially return fewer
   // bars while it downloads the M1 history; still publish every bar received.
   int requested = MathMax(1, CandleCount);
   ResetLastError();
   int copied = CopyRates(_Symbol, PERIOD_M1, 0, requested, rates);
   int copyError = GetLastError();

   if(copied <= 0)
   {
      Print("AL-ZOOL candle history not ready. Symbol=", _Symbol,
            " timeframe=M1 bars=", Bars(_Symbol, PERIOD_M1),
            " copied=", copied, " error=", copyError);
      return;
   }

   int h = FileOpen("ALZOOL\\candles.csv",
      FILE_WRITE | FILE_CSV | FILE_COMMON | FILE_ANSI |
      FILE_SHARE_READ | FILE_SHARE_WRITE,
      ';');
   if(h == INVALID_HANDLE)
   {
      Print("AL-ZOOL candles.csv open failed. error=", GetLastError());
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
   FileFlush(h);
   FileClose(h);

   if(copied < 60)
      Print("AL-ZOOL published ", copied,
            " M1 candles; waiting for at least 60. CopyRates error=", copyError);
}

void WriteResult(const string id, const string result)
{
   int h = FileOpen("ALZOOL\\execution.csv",
      FILE_WRITE | FILE_CSV | FILE_COMMON | FILE_ANSI | FILE_SHARE_READ, ';');
   if(h == INVALID_HANDLE) return;
   FileWrite(h, id, result, GetLastError(), TimeToString(TimeLocal(), TIME_DATE | TIME_SECONDS));
   FileClose(h);
}

void ProcessCommand()
{
   string path = "ALZOOL\\command.csv";
   if(!FileIsExist(path, FILE_COMMON)) return;

   int h = FileOpen(path, FILE_READ | FILE_CSV | FILE_COMMON | FILE_ANSI, ';');
   if(h == INVALID_HANDLE) return;

   string id = FileReadString(h);
   string action = FileReadString(h);
   string symbol = FileReadString(h);
   double volume = FileReadNumber(h);
   FileClose(h);
   FileDelete(path, FILE_COMMON);

   if(id == "" || action == "") return;
   if(symbol != _Symbol || !IsGoldSymbol(_Symbol))
   {
      WriteResult(id, "REJECTED_SYMBOL");
      return;
   }

   trade.SetDeviationInPoints(MaxDeviationPoints);
   bool ok = false;

   if(action == "BUY" || action == "SELL")
   {
      double minLot = SymbolInfoDouble(_Symbol, SYMBOL_VOLUME_MIN);
      double maxLot = SymbolInfoDouble(_Symbol, SYMBOL_VOLUME_MAX);
      double step = SymbolInfoDouble(_Symbol, SYMBOL_VOLUME_STEP);
      if(step <= 0 || volume < minLot || volume > maxLot)
      {
         WriteResult(id, "REJECTED_VOLUME");
         return;
      }
      volume = MathFloor(volume / step) * step;
      int digits = 2;
      if(step < 0.01) digits = 3;
      if(step < 0.001) digits = 4;
      volume = NormalizeDouble(volume, digits);
      if(volume < minLot)
      {
         WriteResult(id, "REJECTED_VOLUME");
         return;
      }
      ok = (action == "BUY")
         ? trade.Buy(volume, _Symbol, 0, 0, 0, "AL-ZOOL")
         : trade.Sell(volume, _Symbol, 0, 0, 0, "AL-ZOOL");
   }
   else if(action == "CLOSE_ALL")
   {
      ok = true;
      for(int i = PositionsTotal() - 1; i >= 0; --i)
      {
         ulong ticket = PositionGetTicket(i);
         if(ticket == 0) continue;
         if(PositionGetString(POSITION_SYMBOL) == _Symbol)
            if(!trade.PositionClose(ticket)) ok = false;
      }
   }
   else
   {
      WriteResult(id, "REJECTED_UNKNOWN_COMMAND");
      return;
   }

   WriteResult(id, ok ? "OK" : "FAILED_" + IntegerToString((int)trade.ResultRetcode()));
}

int OnInit()
{
   if(!IsGoldSymbol(_Symbol))
   {
      Print("AL-ZOOL: attach this bridge to an XAUUSD chart. Current symbol: ", _Symbol);
      return INIT_FAILED;
   }
   FolderCreate("ALZOOL", FILE_COMMON);
   trade.SetDeviationInPoints(MaxDeviationPoints);
   EventSetTimer(MathMax(1, TimerSeconds));
   Print("AL-ZOOL bridge started on ", _Symbol,
         ". Order commands are accepted on the connected trading account.");
   return INIT_SUCCEEDED;
}

void OnDeinit(const int reason) { EventKillTimer(); }

void OnTimer()
{
   ProcessCommand();
   WriteStatus();
   WriteCandles();
}
