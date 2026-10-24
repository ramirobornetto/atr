"""Synthetic PA database shapes shared by upstream/Santodan. No private library data."""
import json,sqlite3,sys,shutil
from pathlib import Path
root=Path(sys.argv[1]);root.mkdir(parents=True,exist_ok=True)
work=root/'writer';work.mkdir()
c=sqlite3.connect(work/'achievement_cache.db');c.execute('pragma journal_mode=wal')
c.executescript('''
CREATE TABLE Games(Id INTEGER PRIMARY KEY,ProviderKey TEXT,ProviderGameId INTEGER,PlayniteGameId TEXT,GameName TEXT);
CREATE TABLE Users(Id INTEGER PRIMARY KEY,ProviderKey TEXT,ExternalUserId TEXT,IsCurrentUser INTEGER,FriendSource TEXT);
CREATE TABLE UserGameProgress(Id INTEGER PRIMARY KEY,GameId INTEGER,UserId INTEGER);
CREATE TABLE AchievementDefinitions(Id INTEGER PRIMARY KEY,GameId INTEGER,ApiName TEXT,DisplayName TEXT,Description TEXT);
CREATE TABLE UserAchievements(UserGameProgressId INTEGER,AchievementDefinitionId INTEGER,Unlocked INTEGER,UnlockTimeUtc TEXT);
''')
c.executemany('insert into Users values(?,?,?,?,?)',[(1,'Steam','test-user',1,None),(2,'Manual','manual',1,None),(3,'Local','local',0,None),(4,'Steam','friend',0,'Steam'),(5,'Steam','second-test-user',1,None)])
def add(game,provider,appid,guid,progress,user,entries):
 c.execute('insert or ignore into Games values(?,?,?,?,?)',(game,provider,appid,guid,f'Fixture {appid}'))
 c.execute('insert into UserGameProgress values(?,?,?)',(progress,game,user))
 for index,(api,unlocked,date) in enumerate(entries):
  key=game*100+index
  c.execute('insert or ignore into AchievementDefinitions values(?,?,?,?,?)',(key,game,api,'Title '+api,'Description '+api))
  c.execute('insert into UserAchievements values(?,?,?,?)',(progress,key,unlocked,date))
g1='00000000-0000-0000-0000-000000000001';g2='00000000-0000-0000-0000-000000000002'
add(1,'Steam',100,g1,1,1,[('A',1,'2024-01-01T00:00:00Z'),('B',0,None),('C',0,None),('D',0,None)])
add(2,'Manual',100,g1,2,2,[('A',1,None),('B',1,None),('C',1,None),('D',1,None)])
add(3,'Local',200,g2,3,3,[('L1',1,'2024-01-01T00:00:00Z'),('L2',1,'not a date'),('L3',0,None)])
add(4,'Steam',300,'00000000-0000-0000-0000-000000000003',4,4,[('FRIEND',1,'2024-01-01T00:00:00Z')])
add(5,'Steam',500,'00000000-0000-0000-0000-000000000005',5,1,[('LOCKED',0,None)])
g6='00000000-0000-0000-0000-000000000006'
add(6,'Steam',600,g6,6,1,[('P1',1,'2024-01-01T00:00:00Z'),('P2',0,None)])
add(6,'Steam',600,g6,7,5,[('P1',1,'2024-02-01T00:00:00Z'),('P2',1,'2024-02-02T00:00:00Z')])
c.commit()
# Copy while writer remains open: all committed tables/rows are in the WAL.
for name in ['achievement_cache.db','achievement_cache.db-wal','achievement_cache.db-shm']:shutil.copy2(work/name,root/name)
c.close()
d=sqlite3.connect(root/'game_custom_data.db');d.execute('CREATE TABLE GameCustomData(PlayniteGameId TEXT PRIMARY KEY,PayloadJson TEXT,UpdatedUtc TEXT)')
link={'SourceKey':'Steam','SourceGameId':'100','UnlockStates':{'A':True,'B':True,'C':True,'D':False,'E':True},'UnlockTimes':{'A':'invalid','B':None,'C':'2024-01-02T00:00:00Z','D':'2024-01-02T00:00:00Z'}}
d.execute('insert into GameCustomData values(?,?,?)',(g1,json.dumps({'SchemaVersion':9,'PlayniteGameId':g1,'ManualLink':link}),'2024-01-02T00:00:00Z'));d.execute('insert into GameCustomData values(?,?,?)',(g2,json.dumps({'SchemaVersion':9,'PlayniteGameId':g2,'AchievementOverrides':{'L1':{'DisplayName':'Edited L1','ClearUnlockTime':True}}}),'2024-01-02T00:00:00Z'));d.commit();d.close()
shutil.rmtree(work)

up=root.parent/'pa-upstream';up.mkdir()
for name in ['achievement_cache.db','achievement_cache.db-wal','achievement_cache.db-shm','game_custom_data.db']:shutil.copy2(root/name,up/name)
u=sqlite3.connect(up/'game_custom_data.db')
for guid,payload in u.execute('select PlayniteGameId,PayloadJson from GameCustomData').fetchall():
 data=json.loads(payload);data['SchemaVersion']=8;u.execute('update GameCustomData set PayloadJson=? where PlayniteGameId=?',(json.dumps(data),guid))
u.commit();u.close()
