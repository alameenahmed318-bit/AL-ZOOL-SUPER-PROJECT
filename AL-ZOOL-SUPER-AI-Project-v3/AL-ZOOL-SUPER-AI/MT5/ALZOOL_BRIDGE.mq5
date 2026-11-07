#property strict
#property version "1.00"
#property description "AL-ZOOL read-only MT5 status bridge"
input int TimerSeconds=1;
int OnInit(){ FolderCreate("ALZOOL",FILE_COMMON); EventSetTimer(MathMax(1,TimerSeconds)); Print("AL-ZOOL Bridge started (read-only)"); return INIT_SUCCEEDED; }
void OnDeinit(const int reason){ EventKillTimer(); }
void OnTimer(){
 int h=FileOpen("ALZOOL\\status.csv",FILE_WRITE|FILE_CSV|FILE_COMMON|FILE_ANSI,';');
 if(h==INVALID_HANDLE){ Print("AL-ZOOL status file error: ",GetLastError()); return; }
 FileWrite(h,(long)AccountInfoInteger(ACCOUNT_LOGIN),AccountInfoString(ACCOUNT_SERVER),
  DoubleToString(AccountInfoDouble(ACCOUNT_BALANCE),2),DoubleToString(AccountInfoDouble(ACCOUNT_EQUITY),2),_Symbol,
  DoubleToString(SymbolInfoDouble(_Symbol,SYMBOL_BID),_Digits),DoubleToString(SymbolInfoDouble(_Symbol,SYMBOL_ASK),_Digits),
  PositionsTotal(),TimeToString(TimeLocal(),TIME_DATE|TIME_SECONDS));
 FileClose(h);
}
