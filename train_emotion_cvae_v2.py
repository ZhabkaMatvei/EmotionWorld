import argparse
from pathlib import Path
import numpy as np
import torch
from torch import nn
from torch.nn import functional as F

PARAM_NAMES = [
    'hill_height', 'terrain_frequency', 'tree_density', 'roughness',
    'water_level', 'fog_density', 'sky_brightness', 'ground_hue',
    'vegetation_scale', 'rock_density', 'terrain_detail', 'color_saturation'
]
# Joy, Sadness, Stress, Calm
PROFILES = np.array([
    [.43,.36,.80,.27,.32,.08,.92,.36,.83,.15,.55,.90],
    [.23,.20,.30,.24,.42,.75,.32,.63,.35,.22,.30,.26],
    [.85,.78,.16,.86,.12,.65,.26,.08,.22,.88,.88,.40],
    [.27,.18,.65,.12,.53,.12,.75,.46,.72,.12,.22,.56],
], dtype=np.float32)

class CVAE(nn.Module):
    def __init__(self, latent_dim=8, cond_dim=4, output_dim=12):
        super().__init__()
        self.latent_dim = latent_dim
        self.encoder = nn.Sequential(
            nn.Linear(output_dim + cond_dim, 96), nn.SiLU(),
            nn.Linear(96, 96), nn.SiLU(),
            nn.Linear(96, 64), nn.SiLU(),
        )
        self.mu = nn.Linear(64, latent_dim)
        self.logvar = nn.Linear(64, latent_dim)
        self.decoder = nn.Sequential(
            nn.Linear(latent_dim + cond_dim, 96), nn.SiLU(),
            nn.Linear(96, 96), nn.SiLU(),
            nn.Linear(96, 64), nn.SiLU(),
            nn.Linear(64, output_dim), nn.Sigmoid()
        )

    def forward(self, x, cond):
        h = self.encoder(torch.cat([x, cond], dim=1))
        mu, logvar = self.mu(h), self.logvar(h).clamp(-8, 8)
        z = mu + torch.randn_like(mu) * torch.exp(0.5 * logvar)
        y = self.decoder(torch.cat([z, cond], dim=1))
        return y, mu, logvar

class ExportGenerator(nn.Module):
    def __init__(self, decoder, latent_dim):
        super().__init__()
        self.decoder = decoder
        self.latent_dim = latent_dim

    def forward(self, input_vector):
        return self.decoder(input_vector)


def make_dataset(n, rng):
    labels = rng.integers(0, 4, size=n)
    conditions = np.eye(4, dtype=np.float32)[labels]
    
    blend_mask = rng.random(n) < 0.30
    other = rng.integers(0, 4, size=n)
    alpha = rng.uniform(.15, .85, size=(n, 1)).astype(np.float32)
    blended = (1-alpha) * conditions + alpha * np.eye(4, dtype=np.float32)[other]
    conditions = np.where(blend_mask[:, None], blended, conditions).astype(np.float32)
    base = conditions @ PROFILES
    
    shared = rng.normal(0, .055, size=(n, 1)).astype(np.float32)
    independent = rng.normal(0, .075, size=(n, len(PARAM_NAMES))).astype(np.float32)
    targets = np.clip(base + shared + independent, 0, 1).astype(np.float32)
    return torch.from_numpy(targets), torch.from_numpy(conditions)


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--epochs', type=int, default=100)
    p.add_argument('--batch-size', type=int, default=256)
    p.add_argument('--output-dir', type=Path, default=Path('.'))
    args = p.parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    torch.manual_seed(42)
    rng = np.random.default_rng(42)
    train_x, train_c = make_dataset(12000, rng)
    val_x, val_c = make_dataset(2000, rng)
    model = CVAE()
    optimizer = torch.optim.AdamW(model.parameters(), lr=1e-3, weight_decay=1e-5)
    scheduler = torch.optim.lr_scheduler.CosineAnnealingLR(optimizer, T_max=args.epochs, eta_min=1e-4)
    loader = torch.utils.data.DataLoader(torch.utils.data.TensorDataset(train_x, train_c), batch_size=args.batch_size, shuffle=True)
    history = []
    best = float('inf')
    for epoch in range(args.epochs):
        model.train()
        beta = min(.002, .002 * (epoch + 1) / 25)
        for x, c in loader:
            optimizer.zero_grad()
            pred, mu, logvar = model(x, c)
            recon = F.mse_loss(pred, x)
            kl = -0.5 * (1 + logvar - mu.square() - logvar.exp()).sum(dim=1).mean()
            loss = recon + beta * kl
            loss.backward()
            nn.utils.clip_grad_norm_(model.parameters(), 1.0)
            optimizer.step()
        scheduler.step()
        model.eval()
        with torch.no_grad():
            pred, mu, logvar = model(val_x, val_c)
            val_recon = F.mse_loss(pred, val_x).item()
            val_kl = (-0.5 * (1 + logvar - mu.square() - logvar.exp()).sum(dim=1).mean()).item()
            val_loss = val_recon + beta * val_kl
        history.append((epoch+1, val_recon, val_kl))
        if val_loss < best:
            best = val_loss
            torch.save(model.state_dict(), args.output_dir / 'emotion_cvae_v2.pt')
        if epoch == 0 or (epoch+1) % 10 == 0:
            print(f'Epoch {epoch+1:3d} | val MSE={val_recon:.5f} | KL={val_kl:.4f} | beta={beta:.4f}')

    model.load_state_dict(torch.load(args.output_dir / 'emotion_cvae_v2.pt', map_location='cpu', weights_only=True))
    model.eval()
    generator = ExportGenerator(model.decoder, 8).eval()
    onnx_path = args.output_dir / 'emotion_generator_v2.onnx'
    dummy = torch.zeros(1, 12, dtype=torch.float32)
    torch.onnx.export(generator, dummy, str(onnx_path), input_names=['input'], output_names=['parameters'], opset_version=11, dynamo=False)
    print('ONNX exported:', onnx_path.resolve())
    print('Input: [1,12] = 8 latent + 4 emotion weights; Output: [1,12]')
    print('Parameter order:', ', '.join(PARAM_NAMES))
    try:
        import onnx
        onnx.checker.check_model(onnx.load(str(onnx_path)))
        print('ONNX check: OK')
    except ImportError:
        print('ONNX checker unavailable; install onnx')
    try:
        import matplotlib
        matplotlib.use('Agg')
        import matplotlib.pyplot as plt
        plt.plot([h[0] for h in history], [h[1] for h in history])
        plt.xlabel('Epoch')
        plt.ylabel('Validation reconstruction MSE')
        plt.title('CVAE v2 training')
        plt.tight_layout()
        plt.savefig(args.output_dir / 'training_curve.png', dpi=160)
        plt.close()
        print('Saved training_curve.png')
    except ImportError:
        pass

if __name__ == '__main__':
    main()
