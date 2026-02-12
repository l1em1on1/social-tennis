const GameDetailPage = ({
  params,
}: {
  readonly params: Promise<{ leagueId: string; gameId: string }>;
}): React.JSX.Element => {
  void params;
  return (
    <main className="flex min-h-screen flex-col p-6">
      <h1 className="text-3xl font-bold">Game Details</h1>
      <p className="text-muted-foreground mt-2">View scores, players, and match details.</p>
    </main>
  );
};

export default GameDetailPage;
