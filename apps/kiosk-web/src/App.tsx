import { PuntiroProvider, Surface } from '@puntiro/ui';
import '@puntiro/ui/styles.css';

export function App() {
  return (
    <PuntiroProvider locale="ru" mode="touch">
      <Surface>
        <main>
          <h1>Puntiro Kiosk</h1>
          <p>Инфраструктура киоска готова.</p>
        </main>
      </Surface>
    </PuntiroProvider>
  );
}
