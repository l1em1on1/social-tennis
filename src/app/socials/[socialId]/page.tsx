const SocialDetailPage = ({
  params,
}: {
  readonly params: Promise<{ socialId: string }>;
}): React.JSX.Element => {
  void params;
  return (
    <main className="flex min-h-screen flex-col p-6">
      <h1 className="text-3xl font-bold">Social Details</h1>
      <p className="text-muted-foreground mt-2">View event details, players, and votes.</p>
    </main>
  );
};

export default SocialDetailPage;
