const LeagueGamesPage = ({
  params,
}: {
  readonly params: Promise<{ leagueId: string }>;
}): React.JSX.Element => {
  void params;
  return (
    <main className="flex min-h-screen flex-col p-6">
      <h1 className="text-3xl font-bold">Your Games</h1>
      <p className="text-muted-foreground mt-2">View and manage your league games.</p>
    </main>
  );
};

export default LeagueGamesPage;
