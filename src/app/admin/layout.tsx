const AdminLayout = ({ children }: { readonly children: React.ReactNode }): React.JSX.Element => {
  return (
    <div className="flex min-h-screen">
      <aside className="w-64 border-r p-4">
        <h2 className="text-lg font-semibold">Admin</h2>
        <nav className="mt-4 flex flex-col gap-2">
          <a href="/admin/users" className="hover:underline">
            Users
          </a>
          <a href="/admin/leagues" className="hover:underline">
            Leagues
          </a>
          <a href="/admin/socials" className="hover:underline">
            Socials
          </a>
        </nav>
      </aside>
      <main className="flex-1 p-6">{children}</main>
    </div>
  );
};

export default AdminLayout;
